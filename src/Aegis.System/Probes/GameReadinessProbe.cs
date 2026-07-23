using System.Management;
using Microsoft.Win32;
using Aegis.Core.Abstractions;
using Aegis.Scanners.Probing;
using Aegis.System.Internal;

namespace Aegis.System.Probes;

/// <summary>
/// Реальный пробник «игровой готовности»: аппаратное планирование видеокарты и её тип (реестр + WMI),
/// признак виртуальной машины, наличие библиотек Visual C++ и DirectX (список установленных программ +
/// системные файлы). Только читает.
/// </summary>
public sealed class GameReadinessProbe : IGameReadinessProbe
{
    private readonly IInstalledProgramsProbe _programs;

    public GameReadinessProbe(IInstalledProgramsProbe programs)
    {
        ArgumentNullException.ThrowIfNull(programs);
        _programs = programs;
    }

    public async Task<GameReadiness> ReadAsync(CancellationToken cancellationToken = default)
    {
        if (!OperatingSystem.IsWindows())
        {
            return new GameReadiness();
        }

        var installed = await _programs.FindAsync(includeHidden: true, cancellationToken).ConfigureAwait(false);
        var names = installed.Select(p => p.Name).ToList();

        return new GameReadiness
        {
            HardwareSchedulingEnabled = RegistryReader.GetDword(RegistryHive.LocalMachine,
                @"SYSTEM\CurrentControlSet\Control\GraphicsDrivers", "HwSchMode") == 2,
            WddmVersion = RegistryReader.GetDword(RegistryHive.LocalMachine,
                @"SYSTEM\CurrentControlSet\Control\GraphicsDrivers\FeatureSetUsage", "WddmVersion_Min") ?? 0,
            HasDiscreteGpu = HasDiscreteGpu(),
            IsVirtualMachine = IsVirtualMachine(),
            HasVisualCppX64 = HasVisualCpp(names, "x64"),
            HasVisualCppX86 = HasVisualCpp(names, "x86"),
            HasDirectXRuntime = HasDirectX(),
        };
    }

    /// <summary>Есть ли отдельная видеокарта: у встроенной в процессор тип преобразователя — «Internal».</summary>
    private static bool HasDiscreteGpu()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT AdapterDACType, Name FROM Win32_VideoController");
            foreach (var item in searcher.Get())
            {
                using var card = (ManagementObject)item;
                var dac = card["AdapterDACType"]?.ToString();
                if (!string.IsNullOrWhiteSpace(dac) && !dac.Contains("Internal", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }
        catch (Exception)
        {
            // WMI не ответил — считаем, что отдельной видеокарты нет: лучше не предлагать твик, чем предложить зря.
        }

        return false;
    }

    /// <summary>Windows внутри виртуальной машины — игровые твики там ничего не дают.</summary>
    private static bool IsVirtualMachine()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT Model, Manufacturer FROM Win32_ComputerSystem");
            foreach (var item in searcher.Get())
            {
                using var system = (ManagementObject)item;
                var text = $"{system["Model"]} {system["Manufacturer"]}";
                if (VirtualMachineMarkers.Any(marker => text.Contains(marker, StringComparison.OrdinalIgnoreCase)))
                {
                    return true;
                }
            }
        }
        catch (Exception)
        {
            // Не определили — считаем, что это настоящий компьютер.
        }

        return false;
    }

    private static readonly string[] VirtualMachineMarkers =
        ["Virtual", "VMware", "VirtualBox", "QEMU", "KVM", "Xen", "Parallels", "Hyper-V"];

    /// <summary>Установлен ли пакет Visual C++ нужной разрядности (в названии программы есть «x64»/«x86»).</summary>
    private static bool HasVisualCpp(IReadOnlyList<string> installedNames, string architecture) =>
        installedNames.Any(name =>
            name.Contains("Visual C++", StringComparison.OrdinalIgnoreCase)
            && name.Contains(architecture, StringComparison.OrdinalIgnoreCase)
            // Пакеты 2015–2022 совместимы между собой; более старые версии игры уже не спрашивают.
            && (name.Contains("2015", StringComparison.Ordinal)
                || name.Contains("2017", StringComparison.Ordinal)
                || name.Contains("2019", StringComparison.Ordinal)
                || name.Contains("2022", StringComparison.Ordinal)));

    /// <summary>
    /// Установлены ли старые библиотеки DirectX. Признак — наличие типичной библиотеки в системной папке:
    /// список установленных программ здесь не помогает, набор ставится «россыпью файлов».
    /// </summary>
    private static bool HasDirectX()
    {
        try
        {
            var system32 = Environment.GetFolderPath(Environment.SpecialFolder.System);
            return File.Exists(Path.Combine(system32, "d3dx9_43.dll"))
                   || File.Exists(Path.Combine(system32, "xinput1_3.dll"));
        }
        catch (Exception)
        {
            return true; // не смогли проверить — не предлагаем установку, чтобы не навязывать лишнее
        }
    }
}
