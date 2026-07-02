using Aegis.Scanners.Probing;
using Aegis.System.Internal;

namespace Aegis.System.Probes;

/// <summary>
/// Ищет задачи планировщика с подозрительной командой. Берёт XML всех задач (schtasks /query /xml), достаёт
/// команду каждой и широким предфильтром отбирает кандидатов (закодированный запуск, скачивание, Temp/AppData,
/// LOLBin). Окончательно классифицирует <see cref="Aegis.Scanners.Threats.SuspiciousTaskScanner"/>. Только читает.
/// </summary>
public sealed class SuspiciousTaskProbe : ISuspiciousTaskProbe
{
    // Широкий предфильтр — что вообще показать сканеру (точную оценку даёт он). По содержимому команды.
    // AppData — кандидат: оттуда идут фоновые автообновления (Opera/Zoom/Yandex), которые юзер может захотеть
    // отключить. Сканер отнесёт их к мягкой категории «Фоновое автообновление» (синяя, не угроза).
    private static readonly string[] Candidates =
    [
        @"\temp\", "%temp%", @"\appdata\", "-enc", "encodedcommand", "frombase64", "downloadstring", "downloadfile",
        "iex", "invoke-expression", "-nop", "-w hidden", "-windowstyle hidden", "mshta", "rundll32", "regsvr32",
        "certutil", "bitsadmin", "wscript", "cscript", "wmic", "installutil", "cmstp", "forfiles", "msdt",
    ];

    public async Task<IReadOnlyList<SuspiciousTask>> FindAsync(CancellationToken cancellationToken = default)
    {
        var xml = await RunSchtasksXmlAsync(cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(xml))
        {
            return [];
        }

        var result = new List<SuspiciousTask>();
        foreach (global::System.Text.RegularExpressions.Match match in
                 global::System.Text.RegularExpressions.Regex.Matches(xml, "<Task[\\s\\S]*?</Task>"))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var task = ParseTask(match.Value);
            if (task is not null && Candidates.Any(c => task.Action.Contains(c, StringComparison.OrdinalIgnoreCase)))
            {
                result.Add(task);
            }
        }

        return result;
    }

    /// <summary>
    /// Запустить «schtasks /query /xml» и прочитать вывод как UTF-16LE — именно в этой кодировке schtasks отдаёт
    /// XML, и без явного указания кириллица в именах задач читается «кракозябрами».
    /// </summary>
    private static async Task<string> RunSchtasksXmlAsync(CancellationToken cancellationToken)
    {
        try
        {
            var startInfo = new global::System.Diagnostics.ProcessStartInfo
            {
                FileName = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "schtasks.exe"),
                Arguments = "/query /xml",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = global::System.Text.Encoding.Unicode,
            };

            using var process = global::System.Diagnostics.Process.Start(startInfo);
            if (process is null)
            {
                return string.Empty;
            }

            process.ErrorDataReceived += static (_, _) => { };
            process.BeginErrorReadLine();
            var output = await process.StandardOutput.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
            return output;
        }
        catch (Exception)
        {
            return string.Empty;
        }
    }

    private static SuspiciousTask? ParseTask(string taskXml)
    {
        try
        {
            var doc = global::System.Xml.Linq.XDocument.Parse(taskXml);

            string JoinByName(string name) => string.Join(' ', doc.Descendants()
                .Where(e => e.Name.LocalName == name)
                .Select(e => e.Value.Trim())
                .Where(static v => v.Length > 0));

            var action = $"{JoinByName("Command")} {JoinByName("Arguments")}".Trim();
            if (action.Length == 0)
            {
                return null;
            }

            var uri = doc.Descendants().FirstOrDefault(e => e.Name.LocalName == "URI")?.Value?.Trim();
            var path = string.IsNullOrWhiteSpace(uri) ? string.Empty : uri;
            var name = path.Length > 0 && path.Contains('\\') ? path[(path.LastIndexOf('\\') + 1)..] : path;

            return new SuspiciousTask { Path = path, Name = name.Length > 0 ? name : "(задача)", Action = action };
        }
        catch (Exception)
        {
            return null;
        }
    }
}
