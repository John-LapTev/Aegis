using System.Diagnostics;
using System.Globalization;
using Microsoft.Data.Sqlite;

namespace Aegis.System.Internal;

/// <summary>
/// Работа с базами браузеров: оценка пустого места и уплотнение файла. База открывается в режиме «только
/// чтение» везде, где не нужна запись, а перед изменением файл копируется — потерять историю или закладки
/// из-за сбоя питания недопустимо.
/// </summary>
internal static class SqliteMaintenance
{
    /// <summary>Сколько байт в файле занимают пустоты (их и освободит уплотнение). 0 — сжимать нечего.</summary>
    public static long EstimateReclaimable(string path)
    {
        try
        {
            using var connection = OpenReadOnly(path);
            var freePages = ReadScalar(connection, "PRAGMA freelist_count;");
            var pageSize = ReadScalar(connection, "PRAGMA page_size;");
            return freePages > 0 && pageSize > 0 ? freePages * pageSize : 0;
        }
        catch (Exception)
        {
            // Файл занят, повреждён или это вовсе не база — просто не предлагаем его сжимать.
            return 0;
        }
    }

    /// <summary>Цела ли база (проверяем и до, и после уплотнения).</summary>
    public static bool IsHealthy(string path)
    {
        try
        {
            using var connection = OpenReadOnly(path);
            using var command = connection.CreateCommand();
            // quick_check — быстрая проверка целостности; полная на больших базах занимает минуты.
            command.CommandText = "PRAGMA quick_check;";
            var result = command.ExecuteScalar()?.ToString();
            return string.Equals(result, "ok", StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>
    /// Уплотняет базу. Перед изменением делает копию файла и при любой неудаче возвращает её на место,
    /// поэтому испортить историю браузера операция не может. Возвращает, сколько байт освободилось
    /// (0 — уплотнение не выполнено).
    /// </summary>
    public static long Compact(string path)
    {
        if (!File.Exists(path) || !IsHealthy(path))
        {
            return 0;
        }

        var sizeBefore = new FileInfo(path).Length;
        var backupPath = path + ".aegis-backup";

        try
        {
            File.Copy(path, backupPath, overwrite: true);

            using (var connection = OpenWritable(path))
            {
                using var command = connection.CreateCommand();
                command.CommandText = "VACUUM;";
                command.ExecuteNonQuery();
            }

            SqliteConnection.ClearAllPools(); // иначе файл останется открытым и «мусорные» файлы не удалятся

            // База после уплотнения обязана быть целой; иначе возвращаем копию и считаем операцию неудачной.
            if (!IsHealthy(path))
            {
                Restore(backupPath, path);
                return 0;
            }

            var sizeAfter = new FileInfo(path).Length;
            SafeDelete(backupPath);
            return Math.Max(0, sizeBefore - sizeAfter);
        }
        catch (Exception)
        {
            Restore(backupPath, path);
            return 0;
        }
    }

    /// <summary>Запущен ли хоть один из процессов браузера (сжимать базы открытого браузера нельзя).</summary>
    public static bool IsAnyProcessRunning(IReadOnlyList<string> processNames)
    {
        foreach (var name in processNames)
        {
            var withoutExtension = Path.GetFileNameWithoutExtension(name);
            try
            {
                var found = Process.GetProcessesByName(withoutExtension);
                try
                {
                    if (found.Length > 0)
                    {
                        return true;
                    }
                }
                finally
                {
                    foreach (var process in found)
                    {
                        process.Dispose();
                    }
                }
            }
            catch (Exception)
            {
                // Не смогли проверить — считаем, что браузер запущен: так безопаснее.
                return true;
            }
        }

        return false;
    }

    private static SqliteConnection OpenReadOnly(string path)
    {
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Mode = SqliteOpenMode.ReadOnly,
            Pooling = false,
        }.ToString());
        connection.Open();
        return connection;
    }

    private static SqliteConnection OpenWritable(string path)
    {
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Mode = SqliteOpenMode.ReadWrite,
            Pooling = false,
        }.ToString());
        connection.Open();
        return connection;
    }

    private static long ReadScalar(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        var value = command.ExecuteScalar();
        return value is null ? 0 : Convert.ToInt64(value, CultureInfo.InvariantCulture);
    }

    /// <summary>Вернуть файл из копии (после неудачного уплотнения).</summary>
    private static void Restore(string backupPath, string path)
    {
        try
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(backupPath))
            {
                File.Copy(backupPath, path, overwrite: true);
                SafeDelete(backupPath);
            }
        }
        catch (Exception)
        {
            // Копия остаётся рядом с базой — данные не потеряны, даже если вернуть их автоматически не вышло.
        }
    }

    private static void SafeDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (Exception)
        {
            // Не критично: лишний файл рядом с базой безвреден.
        }
    }
}
