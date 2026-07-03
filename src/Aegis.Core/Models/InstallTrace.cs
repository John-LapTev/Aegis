namespace Aegis.Core.Models;

/// <summary>
/// Снимок «следов» программы в системе на момент времени — набор файлов/папок и веток реестра в местах,
/// куда обычно ставятся программы. Сравнение снимка ДО и ПОСЛЕ установки показывает, что именно добавил установщик.
/// </summary>
public sealed record InstallSnapshot
{
    /// <summary>Пути к файлам/папкам в наблюдаемых местах.</summary>
    public IReadOnlyList<string> Paths { get; init; } = [];

    /// <summary>Ветки реестра в наблюдаемых местах.</summary>
    public IReadOnlyList<string> RegistryKeys { get; init; } = [];
}

/// <summary>
/// «След установки»: всё, что установщик добавил в систему (файлы/папки + ветки реестра). Записывается при установке
/// с наблюдением и используется при удалении, чтобы вычистить ВСЁ, что оставила программа, — даже то, что её
/// собственный деинсталлятор не убрал.
/// </summary>
public sealed record InstallTrace
{
    /// <summary>Имя программы, к которой относится след.</summary>
    public required string ProgramName { get; init; }

    /// <summary>Когда записан след.</summary>
    public required DateTimeOffset CapturedAt { get; init; }

    /// <summary>Файлы/папки, добавленные установщиком.</summary>
    public IReadOnlyList<string> AddedPaths { get; init; } = [];

    /// <summary>Ветки реестра, добавленные установщиком.</summary>
    public IReadOnlyList<string> AddedRegistryKeys { get; init; } = [];
}
