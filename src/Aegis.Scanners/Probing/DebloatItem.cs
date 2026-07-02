namespace Aegis.Scanners.Probing;

/// <summary>Заведомо лишний элемент Windows (фоновая задача/служба/функция) и его состояние.</summary>
public sealed record DebloatItem
{
    /// <summary>Понятное имя (например, «Передача телеметрии», «Советы Windows»).</summary>
    public required string Name { get; init; }

    /// <summary>Категория («телеметрия», «фоновая задача», «служба», «функция»).</summary>
    public required string Category { get; init; }

    /// <summary>Включён ли сейчас (только включённые имеет смысл предлагать отключить).</summary>
    public required bool Enabled { get; init; }

    /// <summary>Имя службы Windows (ключ реестра в ...\Services), если элемент — служба. Для обратимого отключения.</summary>
    public string? ServiceName { get; init; }

    /// <summary>Полный путь задачи планировщика, если элемент — задача. Для обратимого отключения.</summary>
    public string? TaskName { get; init; }
}
