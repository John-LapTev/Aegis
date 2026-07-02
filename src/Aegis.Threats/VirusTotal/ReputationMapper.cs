using Aegis.Core.Models;

namespace Aegis.Threats.VirusTotal;

/// <summary>Преобразует статистику движков VirusTotal в вердикт. Чистая функция — тестируется напрямую.</summary>
internal static class ReputationMapper
{
    public static ReputationVerdict FromStats(int malicious, int suspicious)
    {
        if (malicious >= 3)
        {
            return ReputationVerdict.Malicious;
        }

        if (malicious >= 1 || suspicious >= 3)
        {
            return ReputationVerdict.Suspicious;
        }

        return ReputationVerdict.Clean;
    }
}
