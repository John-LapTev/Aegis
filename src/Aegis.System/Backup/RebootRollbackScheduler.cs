using Aegis.Core.Abstractions;
using Aegis.Core.Models;
using Microsoft.Win32;

namespace Aegis.System.Backup;

/// <summary>
/// Планирует «проверку после перезагрузки» для рискованных правок: пишет памятку на диск + автозапуск RunOnce
/// (Windows сам запустит программу один раз при следующем входе и удалит запись). При запуске с флагом
/// <c>--confirm-rollback</c> программа покажет окно «всё работает?»; не подтвердят → откат по точке восстановления.
/// ИЗВЕСТНОЕ ОГРАНИЧЕНИЕ (аудит 2026-07-04, требует живого прогона на Win11 — см. docs/ROADMAP.md): RunOnce удаляет
/// запись ДО запуска команды, а манифест requireAdministrator даёт UAC-запрос при входе. Если пользователь отклонит
/// UAC (или запуск сорвётся), запись RunOnce уже израсходована — авто-проверка тихо не выполнится. Страховка остаётся:
/// точка восстановления и точечные бэкапы доступны вручную во вкладке «Бэкапы».
/// </summary>
public sealed class RebootRollbackScheduler : IRebootRollbackScheduler
{
    private const string RunOnceKey = @"Software\Microsoft\Windows\CurrentVersion\RunOnce";
    private const string RunOnceValueName = "AegisRollbackCheck";

    private static string PendingPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Aegis", "pending-rollback.txt");

    public void Schedule(IReadOnlyList<string> backupIds, string description)
    {
        // Нечего откатывать (все правки необратимы, например SFC/DISM) — проверку после ребута не планируем.
        var ids = backupIds?.Where(id => !string.IsNullOrWhiteSpace(id)).ToList() ?? [];
        if (ids.Count == 0)
        {
            return;
        }

        try
        {
            // 1. Памятка на диск (атомарно): первая строка — id бэкапов правок через запятую, остальное — описание.
            Internal.AtomicFile.WriteAllText(PendingPath, string.Join(',', ids) + "\n" + description);

            // 2. Автозапуск один раз при следующем входе — с флагом проверки.
            var exe = Environment.ProcessPath;
            if (!string.IsNullOrEmpty(exe))
            {
                using var key = Registry.CurrentUser.CreateSubKey(RunOnceKey);
                key?.SetValue(RunOnceValueName, $"\"{exe}\" --confirm-rollback");
            }
        }
        catch (Exception)
        {
            // Не критично: просто не будет отложенной проверки (точка восстановления всё равно создана).
        }
    }

    public PendingRollback? GetPending()
    {
        try
        {
            if (!File.Exists(PendingPath))
            {
                return null;
            }

            var parts = File.ReadAllText(PendingPath).Split('\n', 2);
            if (parts.Length < 1 || string.IsNullOrWhiteSpace(parts[0]))
            {
                return null;
            }

            var ids = parts[0].Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (ids.Length == 0)
            {
                return null;
            }

            return new PendingRollback
            {
                BackupIds = ids,
                Description = parts.Length > 1 ? parts[1].Trim() : "изменения в системе",
            };
        }
        catch (Exception)
        {
            return null;
        }
    }

    public void Clear()
    {
        try
        {
            if (File.Exists(PendingPath))
            {
                File.Delete(PendingPath);
            }
        }
        catch (Exception)
        {
            // ignore
        }

        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunOnceKey, writable: true);
            key?.DeleteValue(RunOnceValueName, throwOnMissingValue: false);
        }
        catch (Exception)
        {
            // ignore
        }
    }
}
