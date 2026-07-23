using System;
using System.IO;
using System.Text.Json;
using Aegis.Core;

namespace Aegis.App.ViewModels;

/// <summary>
/// Помнит настройку автоматических проверок и время последней между запусками программы.
/// Обычный файл настроек в профиле пользователя — потеря безопасна, вернутся значения по умолчанию.
/// </summary>
public sealed class AutoScanSettingsStore
{
    private static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Aegis", "auto-scan.json");

    /// <summary>Сохранённое состояние расписания.</summary>
    public sealed record State
    {
        public AutoScanInterval Interval { get; init; } = AutoScanInterval.Off;

        public DateTimeOffset? LastRun { get; init; }
    }

    public State Load()
    {
        try
        {
            if (!File.Exists(FilePath))
            {
                return new State();
            }

            return JsonSerializer.Deserialize<State>(File.ReadAllText(FilePath)) ?? new State();
        }
        catch (Exception)
        {
            return new State();
        }
    }

    public void Save(State state)
    {
        ArgumentNullException.ThrowIfNull(state);

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(state));
        }
        catch (Exception)
        {
            // Не удалось сохранить — не критично, просто настройка не переживёт перезапуск.
        }
    }
}
