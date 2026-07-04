using Aegis.Core.Abstractions;
using Aegis.Core.Models;
using Aegis.System.Fixing;
using Xunit;

namespace Aegis.System.Tests.Fixing;

public sealed class StartupProgramRemoverTests
{
    [Fact]
    public async Task MatchedProgramWithUninstaller_RoutesToUninstaller()
    {
        var uninstaller = new FakeUninstaller();
        var leftovers = new FakeLeftovers();
        var remover = new StartupProgramRemover(
            Probe(new InstalledProgram { Name = "Opera", InstallLocation = "/apps/opera", UninstallCommand = "unins.exe", RegistryKeyPath = "HKLM|64|X" }),
            uninstaller, leftovers);

        var result = await remover.RemoveAsync("/apps/opera/opera.exe", "opera.exe");

        Assert.True(result.Success);
        Assert.True(uninstaller.Called);        // пошёл штатным деинсталлятором
        Assert.False(leftovers.Scanned);        // остатки не трогал — снёс штатно
    }

    [Fact]
    public async Task NoUninstaller_CleansLeftovers()
    {
        // Установщика нет (программа уже удалена) → удаляем найденные остатки по имени, а не спотыкаемся (запрос Ивана 1298).
        var uninstaller = new FakeUninstaller();
        var leftovers = new FakeLeftovers(
            new LeftoverItem { Kind = LeftoverKind.Folder, Path = @"C:\Rave", Display = @"C:\Rave" },
            new LeftoverItem { Kind = LeftoverKind.RegistryKey, Path = @"HKCU\SOFTWARE\Rave", Display = @"HKCU\SOFTWARE\Rave" });
        var remover = new StartupProgramRemover(Probe(), uninstaller, leftovers);

        var result = await remover.RemoveAsync("Rave.exe", "Rave.exe");

        Assert.True(result.Success);
        Assert.False(uninstaller.Called);
        Assert.Equal(2, leftovers.RemovedCount);
        Assert.Equal("Rave", leftovers.ScannedProgram?.Name); // имя без .exe — для поиска следов
        Assert.Contains("Убрал", result.Message);
        Assert.Contains("папку", result.Message); // короткая сводка по типам
    }

    [Fact]
    public async Task BareFileName_MatchedByName_UsesInstallLocationForLeftovers()
    {
        // Из журнала загрузки приходит только «Rave.exe» без пути. Программа найдена по имени (без деинсталлятора) →
        // сканер остатков получает её место установки, чтобы найти папку и почистить (баг Ивана 1289/1298).
        var uninstaller = new FakeUninstaller();
        var leftovers = new FakeLeftovers(new LeftoverItem { Kind = LeftoverKind.Folder, Path = @"C:\Rave", Display = @"C:\Rave" });
        var remover = new StartupProgramRemover(
            Probe(new InstalledProgram { Name = "Rave", InstallLocation = @"C:\Rave", UninstallCommand = null, RegistryKeyPath = "HKCU|64|R" }),
            uninstaller, leftovers);

        var result = await remover.RemoveAsync("Rave.exe", "Rave.exe");

        Assert.True(result.Success);
        Assert.Equal(@"C:\Rave", leftovers.ScannedProgram?.InstallLocation);
    }

    [Fact]
    public async Task NoUninstaller_NoLeftovers_FailsHonestly_WithoutSystemFolderOrAppsDeadEnd()
    {
        // Ни установщика, ни следов → честно (без выдумки «системная» и без бессмысленного дед-энда в «Приложения Windows»).
        var uninstaller = new FakeUninstaller();
        var leftovers = new FakeLeftovers(); // пусто
        var remover = new StartupProgramRemover(Probe(), uninstaller, leftovers);

        var result = await remover.RemoveAsync("Rave.exe", "Rave.exe");

        Assert.False(result.Success);
        Assert.DoesNotContain("системн", result.Message);
        Assert.DoesNotContain("Приложения", result.Message);
        Assert.Contains("уже полностью удалена", result.Message);
    }

    private static FakeProbe Probe(params InstalledProgram[] programs) => new(programs);

    private sealed class FakeProbe(IReadOnlyList<InstalledProgram> programs) : IInstalledProgramsProbe
    {
        public Task<IReadOnlyList<InstalledProgram>> FindAsync(bool includeHidden = false, CancellationToken cancellationToken = default) =>
            Task.FromResult(programs);
    }

    private sealed class FakeUninstaller : IProgramUninstaller
    {
        public bool Called { get; private set; }
        public Task<UninstallResult> UninstallAsync(InstalledProgram program, bool cleanLeftovers, CancellationToken cancellationToken = default)
        {
            Called = true;
            return Task.FromResult(new UninstallResult { Success = true, Message = "удалено" });
        }
    }

    private sealed class FakeLeftovers(params LeftoverItem[] found) : ILeftoverService
    {
        public bool Scanned { get; private set; }
        public InstalledProgram? ScannedProgram { get; private set; }
        public int RemovedCount { get; private set; }

        public Task<IReadOnlyList<LeftoverItem>> ScanAsync(InstalledProgram program, CancellationToken cancellationToken = default)
        {
            Scanned = true;
            ScannedProgram = program;
            return Task.FromResult<IReadOnlyList<LeftoverItem>>(found);
        }

        public Task<int> RemoveAsync(IReadOnlyList<LeftoverItem> items, CancellationToken cancellationToken = default)
        {
            RemovedCount = items.Count;
            return Task.FromResult(items.Count);
        }
    }
}
