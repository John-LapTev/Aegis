using Microsoft.Data.Sqlite;
using Aegis.System.Internal;
using Xunit;

namespace Aegis.System.Tests.Internal;

/// <summary>
/// Уплотнение баз проверяем на настоящей базе во временной папке: важно убедиться, что данные остаются на
/// месте (история и закладки браузера — то, что человек боится потерять больше всего).
/// </summary>
public sealed class SqliteMaintenanceTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "aegis-sqlite-" + Guid.NewGuid().ToString("N"));

    public SqliteMaintenanceTests() => Directory.CreateDirectory(_directory);

    [Fact]
    public void Compact_FreesSpace_AndKeepsData()
    {
        var path = CreateBloatedDatabase(rows: 5000, deleteFrom: 1000);
        var sizeBefore = new FileInfo(path).Length;

        var freed = SqliteMaintenance.Compact(path);

        Assert.True(freed > 0, "Уплотнение должно освободить место");
        Assert.True(new FileInfo(path).Length < sizeBefore);
        // Оставшиеся записи никуда не делись — это главное обещание пользователю.
        Assert.Equal(1000, CountRows(path));
        Assert.True(SqliteMaintenance.IsHealthy(path));
    }

    [Fact]
    public void EstimateReclaimable_SeesFreeSpaceAfterDelete()
    {
        var path = CreateBloatedDatabase(rows: 5000, deleteFrom: 500);

        Assert.True(SqliteMaintenance.EstimateReclaimable(path) > 0);
    }

    [Fact]
    public void EstimateReclaimable_FreshDatabase_IsZero()
    {
        // В базе без удалений пустот нет — предлагать сжатие незачем.
        var path = CreateBloatedDatabase(rows: 200, deleteFrom: null);

        Assert.Equal(0, SqliteMaintenance.EstimateReclaimable(path));
    }

    [Fact]
    public void Compact_LeavesNoTemporaryCopyBehind()
    {
        var path = CreateBloatedDatabase(rows: 3000, deleteFrom: 500);

        SqliteMaintenance.Compact(path);

        Assert.False(File.Exists(path + ".aegis-backup"), "Временная копия должна убираться после успеха");
    }

    [Fact]
    public void Compact_NotADatabase_DoesNothing()
    {
        // Посторонний файл с тем же именем не должен ни ломаться, ни исчезать.
        var path = Path.Combine(_directory, "History");
        File.WriteAllText(path, "это не база данных");

        Assert.Equal(0, SqliteMaintenance.Compact(path));
        Assert.Equal("это не база данных", File.ReadAllText(path));
    }

    [Fact]
    public void Compact_MissingFile_ReturnsZero()
    {
        Assert.Equal(0, SqliteMaintenance.Compact(Path.Combine(_directory, "нет-такого.sqlite")));
    }

    [Fact]
    public void IsHealthy_DetectsBrokenFile()
    {
        var path = Path.Combine(_directory, "broken.sqlite");
        File.WriteAllBytes(path, [1, 2, 3, 4, 5]);

        Assert.False(SqliteMaintenance.IsHealthy(path));
    }

    /// <summary>База с записями, часть которых удалена — так внутри появляются пустоты.</summary>
    private string CreateBloatedDatabase(int rows, int? deleteFrom)
    {
        var path = Path.Combine(_directory, $"History-{Guid.NewGuid():N}.sqlite");

        using (var connection = new SqliteConnection($"Data Source={path};Pooling=False"))
        {
            connection.Open();

            using (var create = connection.CreateCommand())
            {
                create.CommandText = "CREATE TABLE urls (id INTEGER PRIMARY KEY, url TEXT, title TEXT);";
                create.ExecuteNonQuery();
            }

            using var transaction = connection.BeginTransaction();
            using (var insert = connection.CreateCommand())
            {
                insert.CommandText = "INSERT INTO urls (id, url, title) VALUES ($id, $url, $title);";
                var id = insert.CreateParameter();
                id.ParameterName = "$id";
                insert.Parameters.Add(id);
                var url = insert.CreateParameter();
                url.ParameterName = "$url";
                insert.Parameters.Add(url);
                var title = insert.CreateParameter();
                title.ParameterName = "$title";
                insert.Parameters.Add(title);

                for (var i = 0; i < rows; i++)
                {
                    id.Value = i;
                    url.Value = $"https://example.com/page-{i}/" + new string('x', 200);
                    title.Value = "Страница " + i + new string('я', 100);
                    insert.ExecuteNonQuery();
                }
            }

            transaction.Commit();

            if (deleteFrom is int from)
            {
                using var delete = connection.CreateCommand();
                delete.CommandText = "DELETE FROM urls WHERE id >= $from;";
                var parameter = delete.CreateParameter();
                parameter.ParameterName = "$from";
                parameter.Value = from;
                delete.Parameters.Add(parameter);
                delete.ExecuteNonQuery();
            }
        }

        SqliteConnection.ClearAllPools();
        return path;
    }

    private static int CountRows(string path)
    {
        using var connection = new SqliteConnection($"Data Source={path};Mode=ReadOnly;Pooling=False");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM urls;";
        return Convert.ToInt32(command.ExecuteScalar());
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        try
        {
            Directory.Delete(_directory, recursive: true);
        }
        catch (Exception)
        {
            // Временная папка — не критично, если не удалилась.
        }
    }
}
