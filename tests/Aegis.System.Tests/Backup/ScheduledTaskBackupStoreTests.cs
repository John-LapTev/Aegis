using Aegis.System.Backup;
using Xunit;

namespace Aegis.System.Tests.Backup;

/// <summary>
/// Бэкап отключаемой задачи планировщика должен сохраняться (для возврата) и сниматься через Discard.
/// Это персистентность записи (JSON) — проверяется на любой ОС; само включение/отключение задачи
/// (schtasks) — Windows-специфично и здесь не вызывается.
/// </summary>
public sealed class ScheduledTaskBackupStoreTests
{
    [Fact]
    public void Backup_PersistsRecord_AndDiscardRemovesIt()
    {
        var store = new ScheduledTaskBackupStore();
        const string taskPath = @"\Microsoft\Windows\AegisTest\Probe";

        var id = store.Backup(taskPath, "Тестовая задача", "Отключение задачи: Тестовая задача");

        Assert.NotNull(id);
        Assert.Contains(store.List(), r => r.Id == id && r.TaskPath == taskPath);

        store.Discard(id!);
        Assert.DoesNotContain(store.List(), r => r.Id == id);
    }

    [Fact]
    public void Discard_UnknownId_DoesNotThrow()
    {
        var store = new ScheduledTaskBackupStore();
        store.Discard("no-such-id"); // не должно бросать
    }
}
