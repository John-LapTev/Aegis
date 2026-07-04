using Microsoft.Win32;
using Aegis.Core.Abstractions;
using Aegis.Core.Models;
using Aegis.System.Backup;
using Aegis.System.Internal;

namespace Aegis.System.Fixing;

/// <summary>
/// Полное обезвреживание майнера (обратимо, стратегия из docs/security/miner-removal.md): останавливает дерево
/// процессов (с защитой критичных системных), снимает автозапуск (Run) с бэкапом реестра и отправляет файл в
/// карантин. Если файл заперт — планирует удаление при следующей перезагрузке (когда процесса уже нет). Кнопка
/// «Вернуть» восстанавливает ФАЙЛ из карантина, но НЕ возвращает автозапуск — иначе майнер снова бы запускался.
/// </summary>
public sealed class MinerRemovalFix : IFix
{
    private readonly IReadOnlyDictionary<string, string> _data;
    private readonly RegistryBackupStore _registry;
    private readonly QuarantineStore _quarantine;

    public MinerRemovalFix(
        string findingId,
        IReadOnlyDictionary<string, string> data,
        RegistryBackupStore registry,
        QuarantineStore quarantine)
    {
        FindingId = findingId;
        _data = data;
        _registry = registry;
        _quarantine = quarantine;
    }

    public string FindingId { get; }

    public ScanGroup Group => ScanGroup.Threats;

    public async Task<FixOutcome> ApplyAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var done = new List<string>();

            // 1. Остановить процесс майнера и его дерево (у ProcessStopFix — защита критичных процессов из System32).
            if (int.TryParse(_data.GetValueOrDefault(FindingDataKeys.Pid), out var pid) && pid > 0)
            {
                var stop = await new ProcessStopFix(FindingId, pid).ApplyAsync(cancellationToken).ConfigureAwait(false);
                done.Add(stop.Success ? "процесс остановлен" : "процесс уже не работал");
            }

            // 2. Снять автозапуск (Run) — с бэкапом ветки (обратимо), чтобы майнер не вернулся после перезагрузки.
            var registryBackupId = DisableAutostart();
            if (registryBackupId is not null)
            {
                done.Add("убран из автозапуска");
            }

            // 3. Файл — в карантин (обратимо); если заперт — удаление при следующей перезагрузке.
            var path = _data.GetValueOrDefault(FindingDataKeys.Path);
            string? quarantineId = null;
            var rebootNeeded = false;
            if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
            {
                try
                {
                    quarantineId = _quarantine.Quarantine(path, "Майнер: " + Path.GetFileName(path));
                    done.Add("файл убран в карантин");
                }
                catch (Exception)
                {
                    // Файл заперт запущенным процессом — планируем удаление при следующей загрузке.
                    rebootNeeded = PendingDelete.ScheduleDeleteOnReboot(path);
                    done.Add(rebootNeeded ? "файл удалится после перезагрузки" : "файл убрать не удалось");
                }
            }

            var message = "Майнер обезврежен: " + string.Join(", ", done)
                          + (rebootNeeded ? ". Перезагрузи компьютер, чтобы завершить удаление." : ".");

            // «Вернуть» = восстановить файл из карантина (НЕ автозапуск). Если карантина нет — обратимого отката нет.
            return quarantineId is not null
                ? FixOutcome.Ok(quarantineId, rebootNeeded) with { Message = message }
                : FixOutcome.OkWithoutBackup(rebootNeeded) with { Message = message };
        }
        catch (Exception ex)
        {
            return FixOutcome.Failed("Не удалось обезвредить майнер: " + ex.Message);
        }
    }

    /// <summary>Снимает запись автозапуска Run (координаты — в Data находки), с бэкапом ветки. null — записи нет.</summary>
    private string? DisableAutostart()
    {
        if (!_data.TryGetValue(FindingDataKeys.AutostartHive, out var hiveName)
            || !_data.TryGetValue(FindingDataKeys.AutostartSubkey, out var subKey)
            || !_data.TryGetValue(FindingDataKeys.AutostartValue, out var valueName))
        {
            return null;
        }

        var hive = RegistryHiveNames.ToHive(hiveName);
        var backupId = _registry.Backup(hive, subKey, valueName, "Майнер: снятие автозапуска «" + valueName + "»");

        using var baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Default);
        using var key = baseKey.OpenSubKey(subKey, writable: true);
        key?.DeleteValue(valueName, throwOnMissingValue: false);
        return backupId;
    }
}
