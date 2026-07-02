namespace Aegis.Scanners.Probing;

/// <summary>Одна запись из системного файла hosts (домен → адрес), read-only.</summary>
public sealed record HostsEntry
{
    /// <summary>Имя хоста/домен (например, <c>windowsupdate.microsoft.com</c>).</summary>
    public required string Hostname { get; init; }

    /// <summary>Адрес, на который домен перенаправлен.</summary>
    public required string MappedIp { get; init; }
}
