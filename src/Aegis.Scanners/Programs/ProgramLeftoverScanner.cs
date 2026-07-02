using Aegis.Core.Abstractions;
using Aegis.Core.Models;
using Aegis.Scanners.Probing;
using Aegis.Scanners.Internal;

namespace Aegis.Scanners.Programs;

/// <summary>
/// Остатки удалённых программ/игр (в группе <see cref="ScanGroup.Junk"/>, подсекция «Остатки удалённых
/// программ»): только ПУСТЫЕ папки в профиле, которые обычно остаются после удаления программы. Удаление —
/// обратимое, в Корзину Windows (kind=folder-delete). Сознательно НЕ трогаем непустые папки: по имени
/// надёжно отличить «остаток» от настроек/сейвов установленной программы нельзя (ложные срабатывания
/// подрывают доверие). Пустая папка — однозначно безопасный и надёжный сигнал.
/// </summary>
public sealed class ProgramLeftoverScanner : IScanner
{
    private const string Section = "Остатки удалённых программ";

    private readonly ILeftoverProbe _probe;

    public ProgramLeftoverScanner(ILeftoverProbe probe)
    {
        ArgumentNullException.ThrowIfNull(probe);
        _probe = probe;
    }

    public ScanGroup Group => ScanGroup.Junk;

    public async Task<ScanResult> ScanAsync(CancellationToken cancellationToken = default)
    {
        var snapshot = await _probe.ReadAsync(cancellationToken).ConfigureAwait(false);
        var findings = new List<Finding>();

        // Только пустые папки, не относящиеся к установленным программам — данных внутри нет, убрать безопасно.
        foreach (var folder in snapshot.Folders.Where(f => f.IsEmpty && !f.MatchesInstalled))
        {
            findings.Add(new Finding
            {
                Id = "leftover-empty-" + ScanId.ForPath(folder.Path),
                Group = ScanGroup.Junk,
                Severity = Severity.Info,
                Title = $"Пустая папка: {folder.Name}",
                Detail = folder.Path,
                Explain = "Это пустая папка в твоём профиле — обычно остаётся после удаления программы или игры. " +
                          "Внутри ничего нет, поэтому её можно спокойно убрать. Удаление в Корзину — если что, вернёшь.",
                Data = new Dictionary<string, string>
                {
                    ["kind"] = FindingKinds.FolderDelete,
                    ["path"] = folder.Path,
                    ["section"] = Section,
                },
            });
        }

        if (findings.Count == 0)
        {
            findings.Add(new Finding
            {
                Id = "leftover-none",
                Group = ScanGroup.Junk,
                Severity = Severity.Ok,
                Title = "Пустых папок-остатков не найдено",
                Detail = "лишних пустых папок в профиле нет",
                Explain = "Программа не нашла в твоём профиле пустых папок от удалённых программ. Это нормально — порядок есть.",
                Data = new Dictionary<string, string> { ["section"] = Section },
            });
        }

        return new ScanResult { Group = ScanGroup.Junk, Findings = findings };
    }

    // Стабильный уникальный ключ из полного пути (хеш) — чтобы Id не коллизировал у разных папок.
}
