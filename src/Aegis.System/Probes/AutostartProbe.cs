using Microsoft.Win32;
using Aegis.Core.Models;
using Aegis.Scanners.Probing;
using Aegis.System.Internal;

namespace Aegis.System.Probes;

/// <summary>Реальный пробник автозапуска: ветки Run (HKLM/HKCU) и папки автозагрузки. Только читает.</summary>
public sealed class AutostartProbe : IAutostartProbe
{
    private const string RunSubKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";

    public Task<IReadOnlyList<AutostartEntry>> FindAsync(CancellationToken cancellationToken = default)
    {
        var entries = new List<AutostartEntry>();

        ReadRun(entries, Registry.LocalMachine, "HKLM", @"HKLM\...\Run");
        ReadRun(entries, Registry.CurrentUser, "HKCU", @"HKCU\...\Run");
        ReadStartupFolder(entries, Environment.GetFolderPath(Environment.SpecialFolder.Startup));
        ReadStartupFolder(entries, Environment.GetFolderPath(Environment.SpecialFolder.CommonStartup));

        return Task.FromResult<IReadOnlyList<AutostartEntry>>(entries);
    }

    private static void ReadRun(List<AutostartEntry> entries, RegistryKey hive, string hiveName, string source)
    {
        try
        {
            using var key = hive.OpenSubKey(RunSubKey);
            if (key is null)
            {
                return;
            }

            foreach (var name in key.GetValueNames())
            {
                var command = key.GetValue(name)?.ToString();
                if (string.IsNullOrWhiteSpace(command))
                {
                    continue;
                }

                var (signature, publisher) = FileSignatureInspector.Inspect(CommandLine.ExtractExecutablePath(command));
                entries.Add(new AutostartEntry
                {
                    Name = name,
                    Command = command,
                    Location = AutostartLocation.RegistryRun,
                    Source = $"{source}\\{name}",
                    Signature = signature,
                    Publisher = publisher,
                    FixData = new Dictionary<string, string>
                    {
                        [FindingDataKeys.Kind] = FindingKinds.AutostartRun,
                        ["hive"] = hiveName,
                        ["subkey"] = RunSubKey,
                        ["name"] = name,
                    },
                });
            }
        }
        catch (Exception)
        {
            // Ветка недоступна — пропускаем (best-effort).
        }
    }

    private static void ReadStartupFolder(List<AutostartEntry> entries, string folder)
    {
        try
        {
            if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder))
            {
                return;
            }

            foreach (var file in Directory.EnumerateFiles(folder))
            {
                // Ярлык разворачиваем в реальную программу; проверяем подпись ЕЁ, а не самого .lnk.
                var extension = Path.GetExtension(file).ToLowerInvariant();
                string? programPath;
                if (extension == ".lnk")
                {
                    programPath = ShortcutResolver.ResolveTarget(file);
                    if (string.IsNullOrWhiteSpace(programPath) || !File.Exists(programPath))
                    {
                        continue; // не смогли развернуть — не помечаем, чтобы не пугать зря.
                    }
                }
                else if (extension is ".exe" or ".bat" or ".cmd")
                {
                    programPath = file;
                }
                else
                {
                    continue; // desktop.ini и прочее — не автозапуск программы.
                }

                var (signature, publisher) = FileSignatureInspector.Inspect(programPath);
                entries.Add(new AutostartEntry
                {
                    Name = Path.GetFileNameWithoutExtension(file),
                    Command = programPath,
                    Location = AutostartLocation.StartupFolder,
                    Source = $"{folder}\\{Path.GetFileName(file)}",
                    Signature = signature,
                    Publisher = publisher,
                    FixData = new Dictionary<string, string>
                    {
                        [FindingDataKeys.Kind] = FindingKinds.AutostartStartup,
                        ["file"] = file,
                    },
                });
            }
        }
        catch (Exception)
        {
            // Папка недоступна — пропускаем.
        }
    }
}
