namespace Aegis.Scanners.Probing;

/// <summary>Уровень здоровья диска по данным SMART — зоны шкалы 🟢/🟡/🔴.</summary>
public enum SmartHealthLevel
{
    /// <summary>🟢 Всё хорошо — норма.</summary>
    Good,

    /// <summary>🟡 Есть тревожные признаки — стоит присмотреть.</summary>
    Warning,

    /// <summary>🔴 Опасно — диск может скоро выйти из строя.</summary>
    Critical,
}
