namespace Aegis.Scanners.Probing;

/// <summary>Загруженный драйвер, совпавший по SHA-256 с базой опасных драйверов (LOLDrivers).</summary>
public sealed record DangerousDriver
{
    /// <summary>Имя драйвера/файла.</summary>
    public required string Name { get; init; }

    /// <summary>Путь к файлу драйвера.</summary>
    public required string Path { get; init; }

    /// <summary>true — подтверждённый ВРЕДОНОС; false — УЯЗВИМЫЙ (легитимный, но используется в атаках BYOVD).</summary>
    public required bool Malicious { get; init; }
}
