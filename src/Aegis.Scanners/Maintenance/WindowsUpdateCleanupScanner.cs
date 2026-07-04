using Aegis.Core.Abstractions;
using Aegis.Core.Models;

namespace Aegis.Scanners.Maintenance;

/// <summary>
/// Очистка старых компонентов обновлений Windows (хранилище компонентов, как «Очистка диска»/DISM),
/// группа <see cref="ScanGroup.Junk"/>. Это безопасное обслуживание, которое накапливается со временем —
/// предлагаем всегда. Само сжатие выполняет штатный DISM (правка с типом <c>dism-cleanup</c>).
/// </summary>
public sealed class WindowsUpdateCleanupScanner : IScanner
{
    public ScanGroup Group => ScanGroup.Junk;

    public Task<ScanResult> ScanAsync(CancellationToken cancellationToken = default)
    {
        var finding = new Finding
        {
            Id = "junk-windows-update-components",
            Group = ScanGroup.Junk,
            Severity = Severity.Info,
            Title = "Старые компоненты обновлений Windows",
            Detail = "Очистка хранилища компонентов (как «Очистка диска»)",
            Explain = "После обновлений Windows хранит старые версии системных файлов про запас. Их можно безопасно " +
                      "сжать штатным средством Windows — освободится место, нужные файлы останутся на месте. " +
                      "Очистка может занять несколько минут.",
            Data = new Dictionary<string, string> { [FindingDataKeys.Kind] = FindingKinds.DismCleanup },
        };

        return Task.FromResult(new ScanResult { Group = ScanGroup.Junk, Findings = [finding] });
    }
}
