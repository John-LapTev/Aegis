using System.Globalization;
using Aegis.Core.Abstractions;
using Aegis.Core.Models;
using Aegis.Core;
using Aegis.Scanners.Probing;

namespace Aegis.Scanners.Files;

/// <summary>
/// Анализатор места на диске (группа <see cref="ScanGroup.Junk"/>): какие папки занимают больше всего места —
/// чтобы понять, что чистить. Заполненность самих дисков (%) показывается в разделе «Здоровье» (на плитках),
/// поэтому здесь её не дублируем. Только показывает; удаление файлов — отдельные правки.
/// </summary>
public sealed class DiskUsageScanner : IScanner
{
    private readonly IDiskUsageProbe _probe;

    public DiskUsageScanner(IDiskUsageProbe probe)
    {
        ArgumentNullException.ThrowIfNull(probe);
        _probe = probe;
    }

    public ScanGroup Group => ScanGroup.Junk;

    public async Task<ScanResult> ScanAsync(CancellationToken cancellationToken = default)
    {
        var snapshot = await _probe.ReadAsync(cancellationToken).ConfigureAwait(false);

        var findings = snapshot.LargeFolders
            .OrderByDescending(static f => f.SizeBytes)
            .Select(CreateFolderFinding)
            .ToList();

        return new ScanResult { Group = ScanGroup.Junk, Findings = findings };
    }

    /// <summary>Разделитель между элементами списка содержимого (как у списка драйверов).</summary>
    private const char EntrySeparator = '\u0001';

    /// <summary>Разделитель полей элемента: имя␟размер␟папка-ли␟путь.</summary>
    private const char FieldSeparator = '\u001F';

    private static Finding CreateFolderFinding(FolderUsage folder)
    {
        var name = FriendlyName(folder.Kind) ?? LeafName(folder.Path);

        // noBatch: галочка массового выбора на самой папке не нужна — выбор идёт ПОФАЙЛОВО в раскрытом списке.
        var data = new Dictionary<string, string>(StringComparer.Ordinal) { ["noBatch"] = "1" };
        if (folder.Children.Count > 0)
        {
            data["kind"] = FindingKinds.FolderContents;
            data["folder"] = folder.Path;
            data["items"] = SerializeChildren(folder.Children);
        }

        return new Finding
        {
            Id = $"disk-folder-{folder.Path}",
            Group = ScanGroup.Junk,
            Severity = Severity.Info,
            Title = $"{name}: {HumanSize.Format(folder.SizeBytes)}",
            Detail = folder.Path,
            Explain = "Эта папка занимает заметную часть диска. Это твои файлы, а не мусор — поэтому автоматически " +
                      "её не чистим. Нажми «Показать содержимое», отметь галочками ненужное (старые видео, архивы, " +
                      "установщики) и удали — в Корзину (можно вернуть) или навсегда. По клику на файл он откроется. " +
                      "Системные папки без уверенности не трогай.",
            Data = data,
        };
    }

    /// <summary>Сериализация содержимого папки в строку для UI: имя␟размер␟папка-ли␟путь, элементы через U+0001.</summary>
    private static string SerializeChildren(IReadOnlyList<FolderEntry> children) =>
        string.Join(EntrySeparator, children.Select(static c =>
            string.Join(FieldSeparator, c.Name, c.SizeBytes.ToString(CultureInfo.InvariantCulture),
                c.IsDirectory ? "1" : "0", c.Path)));

    /// <summary>Подпись известной папки простыми словами; null — обычная папка (показываем по её имени).</summary>
    private static string? FriendlyName(UserFolderKind kind) => kind switch
    {
        UserFolderKind.Downloads => "Загрузки",
        UserFolderKind.Desktop => "Рабочий стол",
        UserFolderKind.Documents => "Документы",
        UserFolderKind.Pictures => "Изображения",
        UserFolderKind.Music => "Музыка",
        UserFolderKind.Videos => "Видео",
        UserFolderKind.AppData => "Данные программ (AppData)",
        UserFolderKind.OneDrive => "OneDrive (облако)",
        UserFolderKind.UserProfile => "Твоя личная папка (все файлы)",
        _ => null,
    };

    /// <summary>Имя последней папки в пути — кроссплатформенно (путь Windows с «\» режется и на Linux в тестах).</summary>
    private static string LeafName(string path)
    {
        var trimmed = path.TrimEnd('\\', '/');
        var separator = trimmed.LastIndexOfAny(['\\', '/']);
        return separator >= 0 ? trimmed[(separator + 1)..] : trimmed;
    }
}
