namespace Aegis.Scanners.Probing;

/// <summary>Снимок звуковой подсистемы: устройства вывода/записи + найденные «надстройки» звука (улучшайзеры).</summary>
public sealed record AudioSnapshot
{
    /// <summary>Звуковые устройства/драйверы (Realtek, NVIDIA HDMI-аудио и т.п.).</summary>
    public required IReadOnlyList<AudioDeviceInfo> Devices { get; init; }

    /// <summary>Реально установленные службы «улучшайзеров» звука (Nahimic/Dolby/MaxxAudio…), которые есть в системе.</summary>
    public required IReadOnlyList<AudioServiceInfo> EnhancementServices { get; init; }
}

/// <summary>Звуковое устройство/драйвер (read-only).</summary>
public sealed record AudioDeviceInfo
{
    /// <summary>Название устройства/драйвера.</summary>
    public required string Name { get; init; }

    /// <summary>Производитель (Realtek, NVIDIA, Intel, AMD…).</summary>
    public required string Manufacturer { get; init; }
}

/// <summary>Найденная служба «улучшайзера» звука (существует в системе) — кандидат на обратимое отключение.</summary>
public sealed record AudioServiceInfo
{
    /// <summary>Продукт из каталога (например, «Nahimic», «Dolby», «Waves MaxxAudio»).</summary>
    public required string Product { get; init; }

    /// <summary>Имя службы Windows (подтверждённо существует) — для обратимого отключения (Start=4).</summary>
    public required string ServiceName { get; init; }
}
