using Aegis.Core.Models;

namespace Aegis.Core.Abstractions;

/// <summary>
/// Обратимое исправление для находки. Контракт жёстко требует: ПЕРЕД любым изменением
/// системы создаётся бэкап (точка восстановления / экспорт ветки / карантин), и только потом
/// применяется правка (ADR 0002, 0004). Реализации не делают безвозвратных удалений.
/// </summary>
public interface IFix
{
    /// <summary>Идентификатор находки, к которой относится исправление.</summary>
    string FindingId { get; }

    /// <summary>Группа, в которой выполняется правка.</summary>
    ScanGroup Group { get; }

    /// <summary>
    /// Нужна ли «зонтичная» точка восстановления Windows перед этой правкой. По умолчанию да.
    /// Для безопасных удалений файлов (мусор/кэш → Корзина) — нет: их обратимость и так обеспечивает Корзина,
    /// а создание точки восстановления (VSS) медленное и зря тормозит чистку.
    /// </summary>
    bool RequiresSystemRestorePoint => true;

    /// <summary>
    /// Применить исправление. Реализация обязана сначала создать обратимый бэкап через
    /// <see cref="IRestorePointService"/> и вернуть его идентификатор в <see cref="FixOutcome.BackupId"/>.
    /// </summary>
    Task<FixOutcome> ApplyAsync(CancellationToken cancellationToken = default);
}
