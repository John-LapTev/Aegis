using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Aegis.Scanners.Probing;

namespace Aegis.System.Internal;

/// <summary>
/// Определяет статус цифровой подписи файла и (best-effort) издателя. Статус — через WinVerifyTrust
/// (видит и каталоговые подписи Windows, как сам ОС). Издатель — из встроенного сертификата, если есть.
/// </summary>
internal static class FileSignatureInspector
{
    public static (SignatureStatus Status, string? Publisher) Inspect(string? executablePath)
    {
        if (string.IsNullOrWhiteSpace(executablePath) || !File.Exists(executablePath))
        {
            return (SignatureStatus.Unknown, null);
        }

        bool trusted;
        try
        {
            trusted = AuthenticodeTrust.IsTrusted(executablePath);
        }
        catch (Exception)
        {
            // WinVerifyTrust недоступен (не Windows) или ошибка — не делаем выводов.
            return (SignatureStatus.Unknown, null);
        }

        return trusted
            ? (SignatureStatus.Signed, TryGetPublisher(executablePath))
            : (SignatureStatus.Unsigned, null);
    }

    private static string? TryGetPublisher(string path)
    {
        try
        {
            // SYSLIB0057: API устарел в .NET 9, но это простой способ извлечь издателя из встроенной
            // подписи. У файлов с подписью только через каталог встроенного сертификата нет → null.
#pragma warning disable SYSLIB0057
            using var certificate = new X509Certificate2(X509Certificate.CreateFromSignedFile(path));
#pragma warning restore SYSLIB0057
            var name = certificate.GetNameInfo(X509NameType.SimpleName, forIssuer: false);
            return string.IsNullOrWhiteSpace(name) ? null : name;
        }
        catch (CryptographicException)
        {
            return null;
        }
        catch (Exception)
        {
            return null;
        }
    }
}
