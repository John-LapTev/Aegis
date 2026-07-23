using Aegis.Core;
using Aegis.Core.Abstractions;
using Aegis.Core.Models;
using Aegis.Scanners.Internal;
using Aegis.Scanners.Probing;

namespace Aegis.Scanners.Junk;

/// <summary>
/// Раздутые базы браузеров (группа <see cref="ScanGroup.Junk"/>). Браузер хранит историю, закладки и данные
/// форм в файлах-базах; при удалении записей файл не уменьшается — внутри остаются пустоты. Сжатие
/// переупаковывает файл: данные те же, размер меньше, обращения чуть быстрее.
///
/// Показываем только когда браузер закрыт и выигрыш ощутимый — иначе просить человека что-то делать незачем.
/// </summary>
public sealed class BrowserDatabaseScanner : IScanner
{
    private readonly IBrowserDatabaseProbe _probe;

    public BrowserDatabaseScanner(IBrowserDatabaseProbe probe)
    {
        ArgumentNullException.ThrowIfNull(probe);
        _probe = probe;
    }

    public ScanGroup Group => ScanGroup.Junk;

    public async Task<ScanResult> ScanAsync(CancellationToken cancellationToken = default)
    {
        var databases = await _probe.FindAsync(cancellationToken).ConfigureAwait(false);
        var findings = new List<Finding>();

        foreach (var group in databases.GroupBy(d => d.Browser, StringComparer.Ordinal))
        {
            var items = group.ToList();
            var reclaimable = items.Sum(d => d.ReclaimableBytes);
            if (reclaimable < BrowserDatabaseCatalog.MinimumTotalGainBytes)
            {
                continue; // выигрыш меньше порога — не отвлекаем человека
            }

            findings.Add(new Finding
            {
                Id = "browserdb-" + group.Key,
                Group = ScanGroup.Junk,
                Severity = Severity.Info,
                Title = $"{group.Key}: внутренние базы можно сжать",
                Detail = $"освободится примерно {HumanSize.Format(reclaimable)}",
                Explain = "Браузер хранит историю, закладки, значки сайтов и данные форм в служебных файлах-базах. " +
                          "Когда записи оттуда удаляются, файл не уменьшается — внутри остаются пустоты, и за годы " +
                          "он разрастается. Сжатие переупаковывает такой файл: все твои данные остаются на месте, " +
                          "просто исчезают пустоты. История, закладки и пароли не пропадут. " +
                          "Важно: делать это можно только при закрытом браузере — сейчас он закрыт, поэтому кнопка " +
                          "доступна. Перед изменением программа делает копию файла и вернёт её, если что-то пойдёт " +
                          "не так.",
                Data = new Dictionary<string, string>
                {
                    [FindingDataKeys.Kind] = FindingKinds.SqliteVacuum,
                    [FindingDataKeys.Paths] = string.Join("|", items.Select(d => d.Path)),
                    [FindingDataKeys.Bytes] = reclaimable.ToString(),
                    [FindingDataKeys.Section] = "Базы браузеров",
                },
            });
        }

        return new ScanResult { Group = ScanGroup.Junk, Findings = findings };
    }
}
