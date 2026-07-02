using System.Management;
using Aegis.Scanners.Probing;

namespace Aegis.System.Probes;

/// <summary>
/// Читает постоянные подписки WMI из root\subscription: CommandLineEventConsumer (запускает команду) и
/// ActiveScriptEventConsumer (выполняет скрипт). Это редкий и скрытный механизм автозапуска — обычные
/// программы им почти не пользуются, а малварь (включая майнеры) — часто. Только читает.
/// </summary>
public sealed class WmiPersistenceProbe : IWmiPersistenceProbe
{
    public Task<IReadOnlyList<WmiPersistence>> FindAsync(CancellationToken cancellationToken = default)
    {
        var result = new List<WmiPersistence>();

        ReadConsumers(
            result,
            "SELECT Name, CommandLineTemplate, ExecutablePath FROM CommandLineEventConsumer",
            "командная строка",
            consumer => Combine(consumer["ExecutablePath"]?.ToString(), consumer["CommandLineTemplate"]?.ToString()),
            cancellationToken);

        ReadConsumers(
            result,
            "SELECT Name, ScriptText, ScriptFileName FROM ActiveScriptEventConsumer",
            "скрипт",
            consumer => consumer["ScriptText"]?.ToString() is { Length: > 0 } script
                ? script
                : consumer["ScriptFileName"]?.ToString() ?? string.Empty,
            cancellationToken);

        return Task.FromResult<IReadOnlyList<WmiPersistence>>(result);
    }

    private static void ReadConsumers(
        List<WmiPersistence> into,
        string query,
        string kind,
        Func<ManagementObject, string> action,
        CancellationToken cancellationToken)
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(@"root\subscription", query);
            foreach (var item in searcher.Get())
            {
                cancellationToken.ThrowIfCancellationRequested();
                using var consumer = (ManagementObject)item;
                var name = consumer["Name"]?.ToString();
                if (string.IsNullOrWhiteSpace(name))
                {
                    continue;
                }

                into.Add(new WmiPersistence
                {
                    Name = name,
                    Kind = kind,
                    Action = action(consumer).Trim(),
                });
            }
        }
        catch (Exception)
        {
            // root\subscription недоступен (не Windows / нет прав) — просто без находок этого типа.
        }
    }

    private static string Combine(string? executable, string? commandLine) =>
        string.Join(' ', new[] { executable, commandLine }.Where(static s => !string.IsNullOrWhiteSpace(s)));
}
