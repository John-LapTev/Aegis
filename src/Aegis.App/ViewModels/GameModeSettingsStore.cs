using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Aegis.Core.Models;

namespace Aegis.App.ViewModels;

/// <summary>
/// Помнит выбранные настройки игрового режима между запусками (галочки «что делать при включении»).
/// Обычный файл настроек в профиле пользователя: ничего системного здесь не хранится, поэтому потеря файла
/// безопасна — вернутся значения по умолчанию.
/// </summary>
public sealed class GameModeSettingsStore
{
    private static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Aegis", "game-mode-settings.json");

    public GameModeOptions Load()
    {
        try
        {
            if (!File.Exists(FilePath))
            {
                return new GameModeOptions();
            }

            var saved = JsonSerializer.Deserialize<GameModeOptions>(File.ReadAllText(FilePath));
            return saved is null ? new GameModeOptions() : Sanitize(saved);
        }
        catch (Exception)
        {
            return new GameModeOptions();
        }
    }

    public void Save(GameModeOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(options));
        }
        catch (Exception)
        {
            // Не удалось сохранить настройки — не критично, просто спросим галочки заново.
        }
    }

    /// <summary>Имена процессов из файла — только правдоподобные (файл правится вручную).</summary>
    private static GameModeOptions Sanitize(GameModeOptions options) => options with
    {
        CustomGameProcesses = Clean(options.CustomGameProcesses),
    };

    private static IReadOnlyList<string> Clean(IReadOnlyList<string> names)
    {
        var result = new List<string>();
        foreach (var name in names)
        {
            var value = name.Trim();
            if (value.Length is > 0 and <= 260 && value.IndexOfAny(Path.GetInvalidFileNameChars()) < 0)
            {
                result.Add(value);
            }
        }

        return result;
    }
}
