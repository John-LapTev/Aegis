namespace Aegis.Scanners.Stress;

/// <summary>Почему проверка под нагрузкой завершилась — определяет вердикт простыми словами.</summary>
public enum StressAbortReason
{
    /// <summary>Прошла полностью запланированное время — данные собраны, перегрева не было.</summary>
    Completed,

    /// <summary>Авто-стоп: температура подошла к опасному порогу — остановили ради безопасности.</summary>
    OverheatStopped,

    /// <summary>Пользователь нажал «Стоп».</summary>
    Cancelled,

    /// <summary>Не удалось запустить/провести тест (редкая ошибка).</summary>
    Error,
}
