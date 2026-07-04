using System.IO;

namespace Aegis.System.Internal;

/// <summary>
/// Атомарная запись текстового файла: пишем во временный файл рядом, затем переименовываем поверх целевого.
/// Обрыв на записи не оставит целевой файл битым (иначе whitelist/след установки молча терялись бы — аудит 2026-07-04).
/// </summary>
internal static class AtomicFile
{
    public static void WriteAllText(string path, string contents)
    {
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }

        var temp = path + ".tmp";
        File.WriteAllText(temp, contents);
        File.Move(temp, path, overwrite: true); // на одном томе — атомарная замена
    }
}
