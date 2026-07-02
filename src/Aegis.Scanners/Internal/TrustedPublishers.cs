namespace Aegis.Scanners.Internal;

/// <summary>
/// Проверка издателя по точному каноническому имени, а не по префиксу строки.
/// Префикс («начинается на Microsoft») обходится подделкой имени издателя в подписи —
/// для сканера безопасности это недопустимо.
/// </summary>
internal static class TrustedPublishers
{
    private static readonly HashSet<string> Microsoft = new(StringComparer.OrdinalIgnoreCase)
    {
        "Microsoft Corporation",
        "Microsoft Windows",
        "Microsoft Windows Publisher",
    };

    /// <summary>Является ли издатель доверенным системным (Microsoft) — по точному совпадению имени.</summary>
    public static bool IsMicrosoft(string? publisher) =>
        publisher is not null && Microsoft.Contains(publisher);
}
