namespace Aegis.System.Internal;

/// <summary>
/// Удаление файлов/папок в Корзину Windows — ОБРАТИМО (ADR 0002): элемент восстанавливается из Корзины,
/// место освобождается после её очистки. Единая точка для всех правок, удаляющих файлы (мусор,
/// большие/дублирующиеся файлы, остатки программ), чтобы не было прямых безвозвратных <c>File.Delete</c>.
/// Использует <see cref="ShellFileOperation"/> с предупреждением о безвозвратном удалении — если элемент
/// не помещается в Корзину/Корзина недоступна, мы НЕ удаляем тихо и возвращаем <c>false</c>.
/// </summary>
internal static class RecycleBin
{
    /// <summary>
    /// Отправить один файл в Корзину. <c>true</c> — файл в Корзине или его уже нет; <c>false</c> — не
    /// удалось (занят/нет прав) ИЛИ удаление было бы безвозвратным и пользователь отменил.
    /// </summary>
    public static bool TrySend(string path)
    {
        if (!File.Exists(path))
        {
            return true;
        }

        return ShellFileOperation.RecycleToBin(path);
    }

    /// <summary>
    /// Отправить МНОГО файлов в Корзину одним вызовом — для быстрой чистки кэша (тысячи мелких файлов).
    /// Несуществующие отсеиваются. Чанкуем, чтобы строка путей не разрасталась бесконечно.
    /// </summary>
    public static bool TrySendMany(IReadOnlyList<string> paths)
    {
        var existing = paths.Where(File.Exists).ToList();
        if (existing.Count == 0)
        {
            return true;
        }

        const int chunkSize = 4000;
        var ok = true;
        for (var i = 0; i < existing.Count; i += chunkSize)
        {
            var chunk = existing.GetRange(i, Math.Min(chunkSize, existing.Count - i));
            ok &= ShellFileOperation.RecycleManyToBin(chunk);
        }

        return ok;
    }

    /// <summary>
    /// Отправить папку целиком в Корзину — обратимо (для «остатков удалённых программ»). <c>true</c> —
    /// папка в Корзине или её уже нет; <c>false</c> — не удалось ИЛИ удаление было бы безвозвратным и отменено.
    /// </summary>
    public static bool TrySendDirectory(string path)
    {
        if (!Directory.Exists(path))
        {
            return true;
        }

        return ShellFileOperation.RecycleToBin(path);
    }
}
