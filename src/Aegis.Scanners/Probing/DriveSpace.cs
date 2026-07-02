namespace Aegis.Scanners.Probing;

/// <summary>Свободное/общее место на диске (для проверки нехватки места).</summary>
public sealed record DriveSpace
{
    /// <summary>Имя диска (например, «C:»).</summary>
    public required string Name { get; init; }

    /// <summary>Свободно байт.</summary>
    public required long FreeBytes { get; init; }

    /// <summary>Всего байт.</summary>
    public required long TotalBytes { get; init; }

    /// <summary>Доля свободного места 0..1.</summary>
    public double FreeRatio => TotalBytes <= 0 ? 0 : (double)FreeBytes / TotalBytes;
}
