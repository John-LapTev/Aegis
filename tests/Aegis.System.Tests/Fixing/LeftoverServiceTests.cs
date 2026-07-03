using Aegis.Core.Abstractions;
using Aegis.Core.Models;
using Aegis.System.Backup;
using Aegis.System.Fixing;
using Xunit;

namespace Aegis.System.Tests.Fixing;

public sealed class LeftoverServiceTests
{
    [Fact]
    public async Task Scan_FindsRemainingInstallFolder()
    {
        // Вложенная папка (≥3 сегмента), чтобы пройти PathSafety (он не даёт удалять корни/папки первого уровня).
        var dir = Path.Combine(Path.GetTempPath(), "aegis-test", "leftover-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        await File.WriteAllTextAsync(Path.Combine(dir, "data.bin"), "12345");
        try
        {
            var service = new LeftoverService(new FakeTraceStore(), new RegistryKeyBackupStore());
            var program = new InstalledProgram { Name = "Test App", InstallLocation = dir, RegistryKeyPath = "HKLM|64|SOFTWARE\\Test" };

            var found = await service.ScanAsync(program);

            var folder = Assert.Single(found, i => i.Kind == LeftoverKind.Folder);
            Assert.Equal(dir, folder.Path);
            Assert.True(folder.SizeBytes > 0); // размер посчитан
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public async Task Scan_NoLeftovers_ReturnsEmpty()
    {
        var service = new LeftoverService(new FakeTraceStore(), new RegistryKeyBackupStore());
        var program = new InstalledProgram
        {
            Name = "Gone",
            InstallLocation = Path.Combine(Path.GetTempPath(), "aegis-missing-" + Guid.NewGuid().ToString("N")),
            RegistryKeyPath = "HKLM|64|SOFTWARE\\Missing",
        };

        var found = await service.ScanAsync(program);

        Assert.Empty(found); // папки нет, следа нет, ветка реестра недоступна (не Windows/нет)
    }

    private sealed class FakeTraceStore : IInstallTraceStore
    {
        public IReadOnlyList<InstallTrace> LoadAll() => [];
        public InstallTrace? Find(string programName) => null;
        public void Save(InstallTrace trace) { }
        public void Remove(string programName) { }
    }
}
