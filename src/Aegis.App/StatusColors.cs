namespace Aegis.App;

/// <summary>
/// Единые цвета статусов (зелёный/жёлтый/красный/синий и т.п.). Один источник на C#-стороне: и
/// <c>SeverityToBrushConverter</c>, и точки-метки фильтров/бейджей берут отсюда — поменять палитру в одном месте.
/// </summary>
internal static class StatusColors
{
    public const string Ok = "#3FCF8E";       // зелёный — всё хорошо
    public const string Warn = "#F2B855";     // жёлтый — внимание
    public const string Danger = "#F2696B";   // красный — проблема
    public const string Info = "#5FB4F2";     // синий — совет
    public const string Neutral = "#7B8494";  // серый — «все»
    public const string Fixed = "#5FD08A";    // зелёный — «исправлено» (фильтр)

    /// <summary>Приглушённая подложка (тот же цвет с прозрачностью ~14%).</summary>
    public static string Subtle(string hex) => "#24" + hex[1..];
}
