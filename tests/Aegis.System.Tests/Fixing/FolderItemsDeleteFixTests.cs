using Aegis.Core.Models;
using Aegis.System.Fixing;
using Xunit;

namespace Aegis.System.Tests.Fixing;

public sealed class FolderItemsDeleteFixTests
{
    [Fact]
    public async Task ApplyAsync_Permanent_DeletesSelectedFilesAndSubfolders()
    {
        var root = Path.Combine(Path.GetTempPath(), "aegis-folderitems-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var file = Path.Combine(root, "movie.mp4");
        File.WriteAllText(file, "x");
        var sub = Path.Combine(root, "games");
        Directory.CreateDirectory(sub);
        File.WriteAllText(Path.Combine(sub, "inner.txt"), "y");
        var kept = Path.Combine(root, "keep.txt");
        File.WriteAllText(kept, "z");

        try
        {
            // Удаляем только выбранные (file + подпапку), keep.txt не трогаем.
            var fix = new FolderItemsDeleteFix("t", ScanGroup.Junk, [file, sub], permanent: true);

            var outcome = await fix.ApplyAsync();

            Assert.True(outcome.Success);
            Assert.False(File.Exists(file));        // файл удалён навсегда
            Assert.False(Directory.Exists(sub));    // подпапка удалена целиком
            Assert.True(File.Exists(kept));         // невыбранное осталось нетронутым
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task ApplyAsync_MissingPaths_StillSucceeds()
    {
        // Пути уже не существуют (удалены ранее/исчезли) — не ошибка, просто нечего удалять.
        var fix = new FolderItemsDeleteFix("t", ScanGroup.Junk,
            [Path.Combine(Path.GetTempPath(), "aegis-nope-" + Guid.NewGuid().ToString("N"))], permanent: true);

        var outcome = await fix.ApplyAsync();

        Assert.True(outcome.Success);
    }
}
