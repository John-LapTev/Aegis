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
        var force = new FakeForceDelete();
        var remover = new StartupProgramRemover(
            Probe(new InstalledProgram { Name = "Opera", InstallLocation = "/apps/opera", UninstallCommand = "unins.exe", RegistryKeyPath = "HKLM|64|X" }),
            uninstaller, force);

        var result = await remover.RemoveAsync("/apps/opera/opera.exe", "opera.exe");

        Assert.True(result.Success);
        Assert.True(uninstaller.Called);      // пошёл штатным деинсталлятором
        Assert.False(force.Called);           // папку напрямую не сносил
    }

    [Fact]
    public async Task NoMatch_FallsBackToRecycleFolder()
    {
        var uninstaller = new FakeUninstaller();
        var force = new FakeForceDelete();
        var remover = new StartupProgramRemover(Probe(), uninstaller, force);

        var result = await remover.RemoveAsync("/opt/apps/foo/foo.exe", "foo.exe");

        Assert.True(result.Success);
        Assert.False(uninstaller.Called);
        Assert.Equal("/opt/apps/foo", force.DeletedPath); // снёс папку программы в Корзину
    }

    [Fact]
    public async Task MatchedButNoUninstallCommand_FallsBackToFolder()
    {
        var uninstaller = new FakeUninstaller();
        var force = new FakeForceDelete();
        var remover = new StartupProgramRemover(
            Probe(new InstalledProgram { Name = "Foo", InstallLocation = "/opt/apps/foo", UninstallCommand = null, RegistryKeyPath = "HKLM|64|Y" }),
            uninstaller, force);

        await remover.RemoveAsync("/opt/apps/foo/foo.exe", "foo.exe");

        Assert.False(uninstaller.Called);
        Assert.Equal("/opt/apps/foo", force.DeletedPath);
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

    private sealed class FakeForceDelete : IForceDeleteService
    {
        public bool Called { get; private set; }
        public string? DeletedPath { get; private set; }
        public Task<ForceDeleteResult> DeleteAsync(string path, CancellationToken cancellationToken = default)
        {
            Called = true;
            DeletedPath = path;
            return Task.FromResult(new ForceDeleteResult { Success = true, Message = "в Корзину" });
        }
    }
}
