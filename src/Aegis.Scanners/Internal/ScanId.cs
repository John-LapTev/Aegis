namespace Aegis.Scanners.Internal;

/// <summary>
/// Стабильный короткий идентификатор находки из пути (SHA-256 → первые 8 байт в hex). Одинаковый путь → всегда
/// одинаковый Id, поэтому ключ пометки «Безопасно» (whitelist) не «съезжает» между сканами. Вынесено из
/// дублей в ProgramLeftover/StaleFile/SteamLeftover сканерах. `global::System` — из-за коллизии с namespace
/// Aegis.System (см. память namespace-system-collision).
/// </summary>
internal static class ScanId
{
    public static string ForPath(string path)
    {
        var hash = global::System.Security.Cryptography.SHA256.HashData(
            global::System.Text.Encoding.UTF8.GetBytes(path));
        return global::System.Convert.ToHexString(hash, 0, 8);
    }
}
