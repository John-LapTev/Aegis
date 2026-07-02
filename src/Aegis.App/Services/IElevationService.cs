namespace Aegis.App.Services;

/// <summary>Проверка фактических прав администратора в рантайме (ADR 0004).</summary>
public interface IElevationService
{
    /// <summary>Запущено ли приложение с правами администратора.</summary>
    bool IsAdministrator { get; }
}
