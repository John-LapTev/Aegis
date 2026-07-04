using Aegis.Core.Abstractions;
using Aegis.Core.Models;
using Aegis.Scanners.Probing;
using Aegis.Scanners.Internal;

namespace Aegis.Scanners.Programs;

/// <summary>
/// Остатки игр Steam (в группе <see cref="ScanGroup.Junk"/>, подсекция «Остатки удалённых программ»):
/// по базе Steam отличает реально установленные игры от удалённых и находит их хвосты —
/// кэши удалённых игр (безопасно чистить) и следы пиратских копий (осторожно, могут быть сейвы).
/// Удаление — обратимое, в Корзину Windows. Установленные игры НЕ трогаем.
/// </summary>
public sealed class SteamLeftoverScanner : IScanner
{
    // Та же подсекция, что и у пустых папок-остатков — складываем игровые хвосты рядом.
    private const string Section = "Остатки удалённых программ";

    private readonly ISteamLeftoverProbe _probe;

    public SteamLeftoverScanner(ISteamLeftoverProbe probe)
    {
        ArgumentNullException.ThrowIfNull(probe);
        _probe = probe;
    }

    public ScanGroup Group => ScanGroup.Junk;

    public async Task<ScanResult> ScanAsync(CancellationToken cancellationToken = default)
    {
        var snapshot = await _probe.ReadAsync(cancellationToken).ConfigureAwait(false);
        var findings = new List<Finding>();

        foreach (var item in snapshot.Items)
        {
            var isCache = item.Kind == SteamLeftoverKind.OrphanCache;
            var data = new Dictionary<string, string>
            {
                [FindingDataKeys.Kind] = FindingKinds.FolderDelete,
                ["path"] = item.Path,
                [FindingDataKeys.Section] = Section,
            };
            if (!isCache)
            {
                data["noBatch"] = "1"; // следы пираток — только по одному (внутри могут быть сейвы)
            }

            findings.Add(new Finding
            {
                Id = (isCache ? "leftover-steam-cache-" : "leftover-steam-crack-") + ScanId.ForPath(item.Path),
                Group = ScanGroup.Junk,
                Severity = Severity.Info,
                Title = item.Title,
                Detail = item.Path,
                Explain = isCache
                    ? "Это кэш игры, которой уже нет в Steam (шейдеры/файлы совместимости). Игру ты удалил(а), а кэш " +
                      "остался и просто занимает место. Можно спокойно убрать — если переустановишь игру, кэш создастся заново."
                    : "Похоже, это следы пиратской копии игры (папки вроде CODEX/RUNE и эмуляторов Steam). Часто остаются " +
                      "после удаления игры. ВНИМАНИЕ: иногда внутри лежат сохранения — сначала загляни внутрь по кнопке-папке. " +
                      "Удаление в Корзину и только по одному (массовая кнопка такие не трогает — для безопасности).",
                Data = data,
            });
        }

        return new ScanResult { Group = ScanGroup.Junk, Findings = findings };
    }

}
