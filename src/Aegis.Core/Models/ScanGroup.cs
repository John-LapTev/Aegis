namespace Aegis.Core.Models;

/// <summary>
/// Группа сканирования = вкладка в UI. Каждой группе соответствует свой
/// <see cref="Aegis.Core.Abstractions.IScanner"/> (см. docs/VISION.md — 10 групп функций).
/// </summary>
public enum ScanGroup
{
    /// <summary>Общая информация и здоровье системы.</summary>
    System,

    /// <summary>Драйверы: новые версии.</summary>
    Drivers,

    /// <summary>Реестр: проблемы и подозрительные записи.</summary>
    Registry,

    /// <summary>Автозапуск: подозрительные элементы автостарта.</summary>
    Autostart,

    /// <summary>Активные процессы: подозрительные/вредные.</summary>
    Processes,

    /// <summary>Системные настройки: нарушения и неправильные параметры.</summary>
    Settings,

    /// <summary>Мусор: ненужные файлы для очистки.</summary>
    Junk,

    /// <summary>Угрозы: вирусы, трояны, майнеры.</summary>
    Threats,

    /// <summary>Настройки видеокарты (NVIDIA/AMD).</summary>
    Gpu,

    /// <summary>Недостающие библиотеки/драйверы под модель ПК.</summary>
    Missing,

    /// <summary>Здоровье ПК (батарея, диски-SMART, температуры) — показывается в отдельном разделе слева, не вкладкой.</summary>
    Health,
}
