using Aegis.Core.Models;

namespace Aegis.Core.Abstractions;

/// <summary>
/// Тихий фоновый страж: пока приложение свёрнуто в трей, периодически проверяет систему на «живые» угрозы
/// (в первую очередь скрытые майнеры) и поднимает уведомление, если что-то нашёл.
/// </summary>
public interface ISystemGuard
{
    /// <summary>Работает ли страж сейчас.</summary>
    bool IsRunning { get; }

    /// <summary>Срабатывает при новой угрозе (об одном и том же повторно не сообщает).</summary>
    event EventHandler<GuardAlert>? AlertRaised;

    /// <summary>Запустить фоновые проверки.</summary>
    void Start();

    /// <summary>Остановить фоновые проверки.</summary>
    void Stop();
}
