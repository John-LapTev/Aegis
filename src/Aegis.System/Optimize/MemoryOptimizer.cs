using System.Diagnostics;
using System.Runtime.InteropServices;
using Aegis.Core.Abstractions;
using Aegis.Core.Models;

namespace Aegis.System.Optimize;

/// <summary>
/// Честная оптимизация памяти: реальная занятость ОЗУ + ФОНОВЫЕ процессы, которые безопасно закрыть
/// (обновляторы и вспомогательные помощники — сами перезапустятся при необходимости). Одноимённые процессы
/// объединяются в одну строку (не путаем пользователя дублями). НЕ трогаем системные/пользовательские программы.
/// Никаких выдуманных «освободили X ГБ» — реальная память до/после.
/// </summary>
public sealed class MemoryOptimizer : IMemoryOptimizer
{
    /// <summary>Имя процесса (нижний регистр) → понятное имя + объяснение простыми словами.</summary>
    private static readonly Dictionary<string, (string Display, string Desc)> Known = new(StringComparer.OrdinalIgnoreCase)
    {
        ["googleupdate"] = ("Обновление Google", "Проверяет обновления для Chrome и других программ Google. Закрывать безопасно — запустится сам, когда понадобится."),
        ["googlecrashhandler"] = ("Google (отчёты о сбоях)", "Отправляет в Google отчёты, если программа Google дала сбой. На работу не влияет — можно закрыть."),
        ["googlecrashhandler64"] = ("Google (отчёты о сбоях)", "Отправляет в Google отчёты, если программа Google дала сбой. На работу не влияет — можно закрыть."),
        ["microsoftedgeupdate"] = ("Обновление Microsoft Edge", "Проверяет обновления браузера Edge в фоне. Безопасно закрыть."),
        ["adobearm"] = ("Обновление Adobe", "Фоновая проверка обновлений программ Adobe. Безопасно закрыть."),
        ["armsvc"] = ("Обновление Adobe", "Служба обновлений Adobe в фоне. Безопасно закрыть."),
        ["adobeupdateservice"] = ("Обновление Adobe", "Служба обновлений Adobe в фоне. Безопасно закрыть."),
        ["adobeipcbroker"] = ("Помощник Adobe", "Вспомогательный фоновый процесс Adobe. На работу не влияет — можно закрыть."),
        ["adobecollabsync"] = ("Помощник Adobe", "Фоновая синхронизация Adobe. Можно закрыть."),
        ["adobegcclient"] = ("Помощник Adobe", "Фоновая проверка лицензий Adobe. Можно закрыть."),
        ["acrocef"] = ("Помощник Adobe Acrobat", "Фоновый вспомогательный процесс Adobe Acrobat. Можно закрыть."),
        ["jusched"] = ("Обновление Java", "Проверяет обновления Java в фоне. Безопасно закрыть."),
        ["jucheck"] = ("Обновление Java", "Проверяет обновления Java в фоне. Безопасно закрыть."),
        ["ccxprocess"] = ("Помощник Creative Cloud", "Фоновый помощник Adobe Creative Cloud. Безопасно закрыть."),
        ["creative cloud helper"] = ("Помощник Creative Cloud", "Фоновый помощник Adobe Creative Cloud. Безопасно закрыть."),
        ["yourphone"] = ("Связь с телефоном", "Приложение Windows для связи с телефоном (звонки, сообщения, фото). Если не пользуешься — можно закрыть."),
        ["phoneexperiencehost"] = ("Связь с телефоном", "Приложение Windows для связи с телефоном (звонки, сообщения, фото). Если не пользуешься — можно закрыть."),
        ["gamebar"] = ("Игровая панель Xbox", "Панель Xbox поверх игр (запись, скриншоты). Если не нужна — можно закрыть."),
        ["gamebarftserver"] = ("Игровая панель Xbox", "Панель Xbox поверх игр (запись, скриншоты). Если не нужна — можно закрыть."),
        ["widgets"] = ("Виджеты Windows", "Панель виджетов (погода, новости) на панели задач. Если не пользуешься — можно закрыть."),
        ["widgetservice"] = ("Виджеты Windows", "Панель виджетов (погода, новости) на панели задач. Если не пользуешься — можно закрыть."),
    };

    /// <summary>Понятное имя для фонового процесса, если его безопасно закрыть; иначе null.</summary>
    public static string? Classify(string processName) =>
        Known.TryGetValue(processName, out var info) ? info.Display : null;

    public Task<MemoryOptimizerState> ReadAsync(CancellationToken cancellationToken = default) =>
        Task.Run(() =>
        {
            // Собираем безопасно-закрываемые процессы и ГРУППИРУЕМ по понятному имени (одноимённые — одной строкой).
            var groups = new Dictionary<string, (string Desc, string Name, long Memory, List<int> Pids)>(StringComparer.Ordinal);
            foreach (var process in Process.GetProcesses())
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    if (Known.TryGetValue(process.ProcessName, out var info))
                    {
                        if (!groups.TryGetValue(info.Display, out var acc))
                        {
                            acc = (info.Desc, info.Display, 0, []);
                        }

                        acc.Memory += process.WorkingSet64;
                        acc.Pids.Add(process.Id);
                        groups[info.Display] = acc;
                    }
                }
                catch (Exception)
                {
                    // Процесс исчез/нет доступа — пропускаем.
                }
                finally
                {
                    process.Dispose();
                }
            }

            var closeable = groups
                .Select(g => new OptimizableProcess
                {
                    ProcessIds = g.Value.Pids,
                    Name = g.Value.Name,
                    DisplayName = g.Key,
                    Description = g.Value.Desc,
                    MemoryBytes = g.Value.Memory,
                })
                .OrderByDescending(p => p.MemoryBytes)
                .ToList();

            return new MemoryOptimizerState { Memory = ReadMemory(), Closeable = closeable };
        }, cancellationToken);

    public Task<int> StopAsync(IReadOnlyList<int> processIds, CancellationToken cancellationToken = default) =>
        Task.Run(() =>
        {
            var stopped = 0;
            foreach (var pid in processIds)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    using var process = Process.GetProcessById(pid);
                    // Повторная проверка безопасности: закрываем ТОЛЬКО процессы из известного списка.
                    if (Classify(process.ProcessName) is null)
                    {
                        continue;
                    }

                    process.Kill();
                    process.WaitForExit(2000);
                    stopped++;
                }
                catch (Exception)
                {
                    // Уже закрыт / нет прав — пропускаем.
                }
            }

            return stopped;
        }, cancellationToken);

    private static MemorySnapshot ReadMemory()
    {
        var status = new MemoryStatusEx { Length = (uint)Marshal.SizeOf<MemoryStatusEx>() };
        if (GlobalMemoryStatusEx(ref status))
        {
            return new MemorySnapshot
            {
                TotalBytes = (long)status.TotalPhys,
                AvailableBytes = (long)status.AvailPhys,
            };
        }

        return new MemorySnapshot { TotalBytes = 0, AvailableBytes = 0 };
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MemoryStatusEx
    {
        public uint Length;
        public uint MemoryLoad;
        public ulong TotalPhys;
        public ulong AvailPhys;
        public ulong TotalPageFile;
        public ulong AvailPageFile;
        public ulong TotalVirtual;
        public ulong AvailVirtual;
        public ulong AvailExtendedVirtual;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx(ref MemoryStatusEx buffer);
}
