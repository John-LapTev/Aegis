namespace Aegis.Scanners.Probing;

/// <summary>Установленное UWP-приложение (Store/встроенное), которое можно удалить как «лишнее».</summary>
public sealed record AppxApp
{
    /// <summary>Полное имя пакета (для удаления через Remove-AppxPackage).</summary>
    public required string PackageFullName { get; init; }

    /// <summary>Понятное пользователю имя (например, «Игры King (Candy Crush)»).</summary>
    public required string Name { get; init; }

    /// <summary>Категория простыми словами («промо-игра», «промо-приложение»…).</summary>
    public required string Category { get; init; }
}
