using Aegis.Core.Models;
using Aegis.Scanners.Files;
using Aegis.Scanners.Probing;
using Xunit;

namespace Aegis.Scanners.Tests.Files;

public sealed class DiskUsageScannerTests
{
    [Fact]
    public async Task ScanAsync_DoesNotEmitDriveFillFindings_MovedToHealthSection()
    {
        var scanner = new DiskUsageScanner(new FakeProbe(
            drives: [new DriveSpace { Name = "C:", FreeBytes = 50, TotalBytes = 1000 }]));

        var result = await scanner.ScanAsync();

        // Заполненность дисков теперь показывается в разделе «Здоровье» (на плитках) — здесь её больше нет.
        Assert.Empty(result.Findings);
    }

    [Fact]
    public async Task ScanAsync_LargeFolders_BecomeOpenableInfoFindings_SortedBySize()
    {
        var scanner = new DiskUsageScanner(new FakeProbe(
            drives: [],
            folders:
            [
                new FolderUsage { Path = @"C:\Users\i\Documents", SizeBytes = 5_000_000_000 },
                new FolderUsage { Path = @"C:\Users\i\Downloads", SizeBytes = 40_000_000_000 },
            ]));

        var result = await scanner.ScanAsync();

        Assert.Equal(2, result.Findings.Count);
        Assert.All(result.Findings, f => Assert.Equal(Severity.Info, f.Severity));
        // Самая большая папка — первой.
        Assert.Contains("Downloads", result.Findings[0].Detail);
        // Detail = путь → в UI появится кнопка «Открыть папку».
        Assert.Contains(@":\", result.Findings[0].Detail);
    }

    [Fact]
    public async Task ScanAsync_KnownFolders_GetPlainRussianNames()
    {
        var scanner = new DiskUsageScanner(new FakeProbe(
            drives: [],
            folders:
            [
                new FolderUsage { Path = @"C:\Users\i\Downloads", SizeBytes = 40_000_000_000, Kind = UserFolderKind.Downloads },
                new FolderUsage { Path = @"C:\Users\i\Desktop", SizeBytes = 5_000_000_000, Kind = UserFolderKind.Desktop },
            ]));

        var result = await scanner.ScanAsync();

        Assert.StartsWith("Загрузки:", result.Findings[0].Title);
        Assert.StartsWith("Рабочий стол:", result.Findings[1].Title);
    }

    [Fact]
    public async Task ScanAsync_UnknownFolder_UsesItsOwnLeafName()
    {
        var scanner = new DiskUsageScanner(new FakeProbe(
            drives: [],
            folders: [new FolderUsage { Path = @"D:\Games\Steam", SizeBytes = 60_000_000_000 }]));

        var result = await scanner.ScanAsync();

        // Обычная папка (Kind=Other) — подписана своим именем, а не «Папка занимает …».
        Assert.StartsWith("Steam:", result.Findings[0].Title);
    }

    [Fact]
    public async Task ScanAsync_LargeFolders_AreNotBatchSelectable_NoDeadFixButton()
    {
        var scanner = new DiskUsageScanner(new FakeProbe(
            drives: [],
            folders: [new FolderUsage { Path = @"C:\Users\i\Documents", SizeBytes = 5_000_000_000, Kind = UserFolderKind.Documents }]));

        var result = await scanner.ScanAsync();

        // noBatch=1 → в UI у папки нет галочки массового выбора, значит и «мёртвой» кнопки «Исправить».
        Assert.Equal("1", result.Findings[0].Data?.GetValueOrDefault("noBatch"));
    }

    [Fact]
    public async Task ScanAsync_FolderWithChildren_SerializesContentsItems()
    {
        var scanner = new DiskUsageScanner(new FakeProbe(
            drives: [],
            folders:
            [
                new FolderUsage
                {
                    Path = @"C:\Users\i\Downloads",
                    SizeBytes = 40_000_000_000,
                    Kind = UserFolderKind.Downloads,
                    Children =
                    [
                        new FolderEntry { Name = "movie.mp4", Path = @"C:\Users\i\Downloads\movie.mp4", SizeBytes = 30_000_000_000 },
                        new FolderEntry { Name = "games", Path = @"C:\Users\i\Downloads\games", SizeBytes = 10_000_000_000, IsDirectory = true },
                    ],
                },
            ]));

        var data = (await scanner.ScanAsync()).Findings[0].Data!;

        Assert.Equal(FindingKinds.FolderContents, data["kind"]);

        var items = data["items"].Split('\u0001');
        Assert.Equal(2, items.Length);

        var file = items[0].Split('\u001F');
        Assert.Equal("movie.mp4", file[0]);                          // имя
        Assert.Equal("30000000000", file[1]);                        // размер
        Assert.Equal("0", file[2]);                                  // это файл
        Assert.Equal(@"C:\Users\i\Downloads\movie.mp4", file[3]);    // путь

        Assert.Equal("1", items[1].Split('\u001F')[2]);              // вторая запись — папка
    }

    private sealed class FakeProbe(
        IReadOnlyList<DriveSpace> drives,
        IReadOnlyList<FolderUsage>? folders = null) : IDiskUsageProbe
    {
        public Task<DiskUsageSnapshot> ReadAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new DiskUsageSnapshot { Drives = drives, LargeFolders = folders ?? [] });
    }
}
