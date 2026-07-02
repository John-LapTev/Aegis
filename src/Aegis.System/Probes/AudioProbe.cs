using System.Management;
using Aegis.Scanners.Probing;
using Aegis.System.Internal;
using Microsoft.Win32;

namespace Aegis.System.Probes;

/// <summary>
/// Реальный пробник звука: устройства через WMI (Win32_SoundDevice) и наличие служб-улучшайзеров
/// (Nahimic/Dolby/MaxxAudio…) через ключи служб в реестре. Берём только реально существующие службы,
/// поэтому неточные кандидаты безвредны — они просто не находятся. Только читает.
/// </summary>
public sealed class AudioProbe : IAudioProbe
{
    // Кандидаты служб-улучшайзеров: (продукт, возможное имя службы). Существование проверяется по реестру.
    private static readonly (string Product, string Service)[] EnhancementCandidates =
    [
        ("Nahimic", "NahimicService"),
        ("Nahimic", "NahimicSysSvc"),
        ("Dolby", "DolbyDAXAPI"),
        ("Dolby", "DbxSvc"),
        ("Waves MaxxAudio", "WavesSysSvc"),
        ("Sonic Studio", "ASUSOptimization"),
        ("Sound Blaster", "SBCimon"),
    ];

    public Task<AudioSnapshot> ReadAsync(CancellationToken cancellationToken = default)
    {
        var devices = new List<AudioDeviceInfo>();
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT Name, Manufacturer FROM Win32_SoundDevice");
            foreach (var item in searcher.Get())
            {
                using var device = (ManagementObject)item;
                var name = device["Name"]?.ToString();
                if (string.IsNullOrWhiteSpace(name))
                {
                    continue;
                }

                devices.Add(new AudioDeviceInfo
                {
                    Name = name,
                    Manufacturer = device["Manufacturer"]?.ToString() ?? string.Empty,
                });
            }
        }
        catch (Exception)
        {
            // Нет WMI (не Windows) — список устройств остаётся пустым.
        }

        var services = new List<AudioServiceInfo>();
        foreach (var group in EnhancementCandidates.GroupBy(static c => c.Product))
        {
            foreach (var (_, service) in group)
            {
                var key = $@"SYSTEM\CurrentControlSet\Services\{service}";
                if (!RegistryReader.KeyExists(RegistryHive.LocalMachine, key))
                {
                    continue;
                }

                // Показываем только если служба НЕ отключена. Start=4 — отключена (например, мы её уже выключили):
                // тогда повторно не предлагаем, иначе пункт «висит» как невыключенный даже после отключения.
                var start = RegistryReader.GetDword(RegistryHive.LocalMachine, key, "Start");
                if (start is >= 0 and < 4)
                {
                    services.Add(new AudioServiceInfo { Product = group.Key, ServiceName = service });
                    break;
                }
            }
        }

        return Task.FromResult(new AudioSnapshot { Devices = devices, EnhancementServices = services });
    }
}
