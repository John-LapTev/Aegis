using System.Diagnostics;
using Aegis.Core.Abstractions;
using Aegis.Core.Models;
using Aegis.System.Internal;

namespace Aegis.System.Fixing;

/// <summary>
/// «Грубое» удаление файла/папки в Корзину: сначала пробует переместить как есть; если мешает другой процесс —
/// завершает мешающие процессы (через Restart Manager) и повторяет. Критичные системные процессы и сам Aegis
/// НИКОГДА не трогаются (иначе можно уронить Windows). Удаление — в Корзину (обратимо), не насовсем.
/// </summary>
public sealed class ForceDeleteService : IForceDeleteService
{
    // Процессы, которые нельзя завершать НИ ПРИ КАКИХ условиях — их убийство роняет/перезагружает Windows.
    private static readonly HashSet<string> CriticalProcesses = new(StringComparer.OrdinalIgnoreCase)
    {
        "system", "idle", "registry", "smss", "csrss", "wininit", "winlogon", "services",
        "lsass", "lsaiso", "fontdrvhost", "dwm", "explorer", "svchost", "aegis",
    };

    public Task<ForceDeleteResult> DeleteAsync(string path, CancellationToken cancellationToken = default) =>
        Task.Run(() => Delete(path, cancellationToken), cancellationToken);

    private static ForceDeleteResult Delete(string path, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return ForceDeleteResult.Failed("Путь не указан.");
        }

        var isDirectory = Directory.Exists(path);
        if (!isDirectory && !File.Exists(path))
        {
            return ForceDeleteResult.Failed("Файл или папка не найдены — возможно, их уже нет.");
        }

        // 1) Пробуем сразу — вдруг ничто не держит.
        if (TryRecycle(path, isDirectory))
        {
            return new ForceDeleteResult { Success = true, Message = "Перемещено в Корзину." };
        }

        // 2) Держит другой процесс — завершаем мешающие (кроме критичных системных и самого Aegis).
        var killed = KillLockers(path, cancellationToken);

        // 3) Повторяем удаление.
        if (TryRecycle(path, isDirectory))
        {
            var who = killed.Count > 0 ? $" Пришлось закрыть: {string.Join(", ", killed)}." : string.Empty;
            return new ForceDeleteResult { Success = true, Message = "Перемещено в Корзину." + who, KilledProcesses = killed };
        }

        var lockers = FileLockInspector.GetLockingProcessNames(path);
        var tail = lockers.Count > 0
            ? $" Его держит: {string.Join(", ", lockers)} — это защищённый или системный процесс, закрывать его небезопасно."
            : " Возможно, он защищён системой.";
        return ForceDeleteResult.Failed("Не удалось освободить файл." + tail);
    }

    private static bool TryRecycle(string path, bool isDirectory) =>
        isDirectory ? RecycleBin.TrySendDirectory(path) : RecycleBin.TrySend(path);

    private static IReadOnlyList<string> KillLockers(string path, CancellationToken cancellationToken)
    {
        var killed = new List<string>();
        var ownPid = Environment.ProcessId;

        foreach (var pid in FileLockInspector.GetLockingProcessIds(path))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (pid == ownPid || pid <= 4)
            {
                continue; // сам Aegis и системные псевдо-процессы (System/Idle) — не трогаем
            }

            try
            {
                using var process = Process.GetProcessById(pid);
                var name = process.ProcessName;
                if (CriticalProcesses.Contains(name))
                {
                    continue; // критичный системный процесс — закрывать нельзя
                }

                process.Kill(entireProcessTree: true);
                process.WaitForExit(3000);
                if (!killed.Contains(name))
                {
                    killed.Add(name);
                }
            }
            catch (Exception)
            {
                // Процесс уже завершился / нет прав — пропускаем.
            }
        }

        return killed;
    }
}
