using System.Management;
using Aegis.Scanners.Probing;
using Aegis.System.Internal;

namespace Aegis.System.Probes;

/// <summary>
/// Ищет службы Windows, запускающиеся из «неправильных» мест: Temp, AppData, папки пользователя (Загрузки/Рабочий
/// стол/Public). Обычные программы ставят службы в Program Files / System32; запуск службы из этих папок —
/// сильный признак малвари/майнера. Проверяет и подпись исполняемого файла. Только читает.
/// </summary>
public sealed class SuspiciousServiceProbe : ISuspiciousServiceProbe
{
    public Task<IReadOnlyList<SuspiciousService>> FindAsync(CancellationToken cancellationToken = default)
    {
        var result = new List<SuspiciousService>();

        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT Name, DisplayName, PathName FROM Win32_Service");
            foreach (var item in searcher.Get())
            {
                cancellationToken.ThrowIfCancellationRequested();
                using var service = (ManagementObject)item;

                var path = ExtractExecutablePath(service["PathName"]?.ToString());
                if (path is null)
                {
                    continue;
                }

                var reason = SuspiciousLocationReason(path);
                if (reason is null)
                {
                    continue; // обычная защищённая папка — служба не подозрительна.
                }

                var name = service["Name"]?.ToString() ?? Path.GetFileNameWithoutExtension(path);
                result.Add(new SuspiciousService
                {
                    Name = name,
                    DisplayName = service["DisplayName"]?.ToString() ?? name,
                    BinaryPath = path,
                    Signed = FileSignatureInspector.Inspect(path).Status == SignatureStatus.Signed,
                    Reason = reason,
                });
            }
        }
        catch (Exception)
        {
            return Task.FromResult<IReadOnlyList<SuspiciousService>>([]);
        }

        return Task.FromResult<IReadOnlyList<SuspiciousService>>(result);
    }

    /// <summary>Достать путь к .exe из PathName службы (может быть в кавычках и с аргументами).</summary>
    private static string? ExtractExecutablePath(string? pathName)
    {
        if (string.IsNullOrWhiteSpace(pathName))
        {
            return null;
        }

        var value = pathName.Trim();
        if (value.StartsWith('"'))
        {
            var end = value.IndexOf('"', 1);
            return end > 1 ? value[1..end] : null;
        }

        // Без кавычек: путь до «.exe» (далее — аргументы).
        var exeIndex = value.IndexOf(".exe", StringComparison.OrdinalIgnoreCase);
        return exeIndex > 0 ? value[..(exeIndex + 4)] : value;
    }

    /// <summary>Понятная причина, если путь в подозрительной (пользовательски-доступной) папке; иначе null.</summary>
    private static string? SuspiciousLocationReason(string path)
    {
        var lower = path.ToLowerInvariant();

        if (lower.Contains(@"\temp\", StringComparison.Ordinal) || lower.Contains(@"\windows\temp\", StringComparison.Ordinal))
        {
            return "запускается из временной папки (Temp)";
        }

        if (lower.Contains(@"\appdata\", StringComparison.Ordinal))
        {
            return "запускается из папки приложений пользователя (AppData)";
        }

        if (lower.Contains(@"\users\public\", StringComparison.Ordinal)
            || lower.Contains(@"\downloads\", StringComparison.Ordinal)
            || lower.Contains(@"\desktop\", StringComparison.Ordinal))
        {
            return "запускается из пользовательской папки (Загрузки/Рабочий стол/Общие)";
        }

        return null;
    }
}
