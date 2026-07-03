namespace Aegis.Core.Abstractions;

/// <summary>
/// Как давно пользователь не трогал клавиатуру и мышь. Нужно для поведенческих эвристик:
/// скрытый майнер грузит компьютер, ПОКА человек отошёл, — а не когда тот сам играет или рендерит.
/// </summary>
public interface IUserActivityProbe
{
    /// <summary>Сколько времени прошло с последнего ввода пользователя. <see cref="TimeSpan.Zero"/> — если определить не удалось.</summary>
    TimeSpan GetIdleDuration();
}
