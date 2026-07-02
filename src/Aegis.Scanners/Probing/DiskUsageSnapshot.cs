namespace Aegis.Scanners.Probing;

/// <summary>Снимок использования диска: заполненность дисков + крупнейшие папки (анализатор места).</summary>
public sealed record DiskUsageSnapshot
{
    /// <summary>Фиксированные диски и их заполненность.</summary>
    public required IReadOnlyList<DriveSpace> Drives { get; init; }

    /// <summary>Крупнейшие папки в профиле пользователя (что занимает место).</summary>
    public required IReadOnlyList<FolderUsage> LargeFolders { get; init; }
}
