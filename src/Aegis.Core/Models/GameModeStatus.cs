namespace Aegis.Core.Models;

/// <summary>Текущее состояние игрового режима — для интерфейса (кнопка «Включить»/«Выключить» и подписи).</summary>
public sealed record GameModeStatus
{
    /// <summary>Включён ли режим прямо сейчас.</summary>
    public required bool IsActive { get; init; }

    /// <summary>Когда включён (null — выключен).</summary>
    public DateTimeOffset? ActivatedAt { get; init; }

    /// <summary>Название игры, из-за которой режим включился сам (null — включали вручную).</summary>
    public string? TriggeredByGame { get; init; }

    /// <summary>Что именно сейчас применено — списком простыми словами, для показа человеку.</summary>
    public IReadOnlyList<string> AppliedActions { get; init; } = [];

    /// <summary>Режим выключен.</summary>
    public static readonly GameModeStatus Inactive = new() { IsActive = false };
}

/// <summary>Итог включения или выключения игрового режима.</summary>
public sealed record GameModeResult
{
    public required bool Success { get; init; }

    /// <summary>Что удалось сделать — понятными фразами («Приостановлены 4 службы»).</summary>
    public IReadOnlyList<string> Applied { get; init; } = [];

    /// <summary>
    /// Что сделать не получилось. Режим считается включённым и при частичном успехе — тогда эти строки
    /// показываются человеку, чтобы «включено» не выглядело обещанием, которое не выполнено.
    /// </summary>
    public IReadOnlyList<string> Failed { get; init; } = [];

    /// <summary>Сообщение об ошибке для человека (на русском), если не удалось вообще ничего.</summary>
    public string? Message { get; init; }

    public static GameModeResult Ok(IReadOnlyList<string> applied, IReadOnlyList<string>? failed = null) =>
        new() { Success = true, Applied = applied, Failed = failed ?? [] };

    public static GameModeResult Error(string message) =>
        new() { Success = false, Message = message };
}
