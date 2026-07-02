using Aegis.Core.Abstractions;
using Aegis.Core.Models;
using Aegis.Scanners.Probing;

namespace Aegis.Scanners.Audio;

/// <summary>
/// Звук (в группе <see cref="ScanGroup.Drivers"/>): простыми словами объясняет, как устроен звук на ПК
/// (встроенный = колонки+микрофон, NVIDIA/Intel = звук в монитор по HDMI), и даёт рекомендации по
/// «улучшайзерам» (Nahimic/Dolby/MaxxAudio…), которые иногда портят звук/микрофон. Отключение — обратимое
/// (служба переводится в Start=4 с бэкапом; «Откатить» в разделе «Бэкапы»). Рабочее само не трогаем.
/// </summary>
public sealed class AudioScanner : IScanner
{
    private readonly IAudioProbe _probe;

    public AudioScanner(IAudioProbe probe)
    {
        ArgumentNullException.ThrowIfNull(probe);
        _probe = probe;
    }

    public ScanGroup Group => ScanGroup.Drivers;

    public async Task<ScanResult> ScanAsync(CancellationToken cancellationToken = default)
    {
        var snapshot = await _probe.ReadAsync(cancellationToken).ConfigureAwait(false);
        var findings = new List<Finding>();

        var setup = DescribeSetup(snapshot.Devices);
        if (setup is not null)
        {
            findings.Add(setup);
        }

        foreach (var service in snapshot.EnhancementServices)
        {
            findings.Add(CreateEnhancerFinding(service));
        }

        if (snapshot.EnhancementServices.Count == 0 && snapshot.Devices.Count > 0)
        {
            findings.Add(new Finding
            {
                Id = "audio-enhancers-none",
                Group = ScanGroup.Drivers,
                Severity = Severity.Ok,
                Title = "Лишних «улучшайзеров» звука не найдено",
                Detail = "Звук обрабатывается стандартно",
                Explain = "На компьютере не найдено программ-надстроек, которые любят портить звук или мешать " +
                          "микрофону (Nahimic, Dolby и подобные). Это хорошо — звук работает «как есть».",
            });
        }

        return new ScanResult { Group = ScanGroup.Drivers, Findings = findings };
    }

    /// <summary>Одна понятная находка «как устроен звук»: что за колонки/микрофон и что за HDMI-звук.</summary>
    private static Finding? DescribeSetup(IReadOnlyList<AudioDeviceInfo> devices)
    {
        if (devices.Count == 0)
        {
            return null;
        }

        var hasOnboard = devices.Any(IsOnboard);
        var hasDisplay = devices.Any(IsDisplayAudio);

        var parts = new List<string>();
        if (hasOnboard)
        {
            parts.Add("• Встроенный звук (колонки, наушники в разъём и микрофон) — это основное аудио.");
        }

        if (hasDisplay)
        {
            parts.Add("• Звук видеокарты (NVIDIA/Intel/AMD) — это звук в монитор или телевизор по кабелю HDMI/DisplayPort. " +
                      "К микрофону он отношения не имеет.");
        }

        var explain = "Несколько звуковых устройств — это норма, а не ошибка. Каждое отвечает за своё:\n" +
                      (parts.Count > 0 ? string.Join("\n", parts) : "Звуковые устройства найдены и работают.") +
                      "\n\nЕсли в какой-то программе (например, Discord) звук или микрофон работают не так — чаще всего " +
                      "дело не в «конфликте драйверов», а в выборе устройства: проверь, какое устройство выбрано в настройках " +
                      "звука и в самой программе.";

        return new Finding
        {
            Id = "audio-setup",
            Group = ScanGroup.Drivers,
            Severity = Severity.Ok,
            Title = "Звук: как он устроен на твоём компьютере",
            Detail = $"звуковых устройств: {devices.Count}",
            Explain = explain,
        };
    }

    private static Finding CreateEnhancerFinding(AudioServiceInfo service) => new()
    {
        Id = $"audio-enhancer-{Sanitize(service.Product)}",
        Group = ScanGroup.Drivers,
        Severity = Severity.Warning,
        Title = $"Надстройка звука: {service.Product}",
        Detail = "может влиять на звук и микрофон",
        Explain = ImpactFor(service.Product),
        Data = new Dictionary<string, string>
        {
            ["kind"] = FindingKinds.ServiceDisable,
            ["service"] = service.ServiceName,
        },
    };

    private static bool IsDisplayAudio(AudioDeviceInfo device)
    {
        var text = $"{device.Manufacturer} {device.Name}";
        return Contains(text, "NVIDIA") || Contains(text, "Intel(R) Display") || Contains(text, "Intel Display")
               || Contains(text, "AMD High Definition") || Contains(text, "Display Audio") || Contains(text, "HDMI");
    }

    private static bool IsOnboard(AudioDeviceInfo device)
    {
        var text = $"{device.Manufacturer} {device.Name}";
        return Contains(text, "Realtek") || Contains(text, "Conexant") || Contains(text, "Cirrus")
               || Contains(text, "IDT") || Contains(text, "SmartSound")
               || (!IsDisplayAudio(device) && !Contains(text, "Virtual") && !Contains(text, "Steam"));
    }

    private static string ImpactFor(string product) => product switch
    {
        "Nahimic" =>
            "«Nahimic» — программа-надстройка для звука (объёмный звук, шумоподавление микрофона). Часто стоит на " +
            "игровых ноутбуках. Известна тем, что мешает записи и стримингу (например, OBS) и иногда искажает " +
            "микрофон в Discord и играх. Если со звуком/микрофоном бывают странности — её можно безопасно отключить " +
            "(всё обратимо, «Откатить» в разделе «Бэкапы»). Если всё хорошо — можно оставить.",
        "Dolby" =>
            "«Dolby» — обработка звука для объёмного звучания. Иногда меняет звук в худшую сторону или конфликтует " +
            "с играми. Если звук кажется хуже, чем без неё — можно отключить (обратимо). Изменения вступят в силу " +
            "после перезагрузки.",
        "Waves MaxxAudio" =>
            "«Waves MaxxAudio» — надстройка звука (часто на ноутбуках Dell). Иногда делает микрофон тихим или " +
            "искажает звук. Если есть проблемы со звуком/микрофоном — можно отключить (обратимо).",
        "Sonic Studio" =>
            "«Sonic Studio» — звуковые эффекты (часто на технике ASUS). Известны проблемы с микрофоном. Если " +
            "микрофон или звук барахлит — можно отключить (обратимо).",
        "Sound Blaster" =>
            "«Sound Blaster» — звуковые эффекты Creative. Иногда конфликтует с другими программами звука. Если есть " +
            "проблемы — можно отключить (обратимо).",
        _ =>
            $"«{product}» — программа-надстройка, которая обрабатывает звук. Иногда такие программы ухудшают звук " +
            "или мешают микрофону в играх и Discord. Если замечаешь проблемы — можно отключить (обратимо).",
    };

    private static bool Contains(string haystack, string needle) =>
        haystack.Contains(needle, StringComparison.OrdinalIgnoreCase);

    private static string Sanitize(string value) =>
        new(value.Where(char.IsLetterOrDigit).Take(40).ToArray());
}
