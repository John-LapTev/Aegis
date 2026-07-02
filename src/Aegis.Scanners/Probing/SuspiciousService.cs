namespace Aegis.Scanners.Probing;

/// <summary>
/// Служба Windows, запускающаяся из «неправильного» места (Temp/AppData/папки пользователя) — обычные программы
/// ставят службы в защищённые папки, а так часто прячется малварь/майнеры. Read-only — классифицирует сканер.
/// </summary>
public sealed record SuspiciousService
{
    /// <summary>Системное имя службы.</summary>
    public required string Name { get; init; }

    /// <summary>Отображаемое имя службы.</summary>
    public required string DisplayName { get; init; }

    /// <summary>Путь к исполняемому файлу службы.</summary>
    public required string BinaryPath { get; init; }

    /// <summary>Подписан ли исполняемый файл (без подписи — тревожнее).</summary>
    public required bool Signed { get; init; }

    /// <summary>Почему служба подозрительна (понятная причина — напр. «запускается из временной папки»).</summary>
    public required string Reason { get; init; }
}
