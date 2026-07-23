using Microsoft.Win32;
using Aegis.Scanners.Internal;
using Aegis.Scanners.Probing;

namespace Aegis.System.Probes;

/// <summary>
/// Реальный пробник переменной <c>Path</c>: читает системный и пользовательский списки прямо из реестра
/// (без раскрытия переменных — важно сохранить исходный вид) и сообщает записи, ведущие в несуществующие
/// папки. Только читает.
/// </summary>
public sealed class EnvironmentPathProbe : IEnvironmentPathProbe
{
    /// <summary>Системный список (общий для всех пользователей).</summary>
    internal const string MachineKey = @"SYSTEM\CurrentControlSet\Control\Session Manager\Environment";

    /// <summary>Пользовательский список.</summary>
    internal const string UserKey = "Environment";

    public Task<IReadOnlyList<BrokenPathEntry>> ReadBrokenAsync(CancellationToken cancellationToken = default)
    {
        var broken = new List<BrokenPathEntry>();

        if (!OperatingSystem.IsWindows())
        {
            return Task.FromResult<IReadOnlyList<BrokenPathEntry>>(broken);
        }

        Collect(Registry.LocalMachine, MachineKey, "HKLM", broken);
        Collect(Registry.CurrentUser, UserKey, "HKCU", broken);

        return Task.FromResult<IReadOnlyList<BrokenPathEntry>>(broken);
    }

    private static void Collect(RegistryKey root, string subKey, string hiveName, List<BrokenPathEntry> into)
    {
        try
        {
            using var key = root.OpenSubKey(subKey);
            // Без раскрытия: в Path штатно живут %SystemRoot% и подобное, их нельзя «зафиксировать» при записи.
            var value = key?.GetValue("Path", null, RegistryValueOptions.DoNotExpandEnvironmentNames)?.ToString();

            foreach (var missing in PathListEditor.FindMissing(value, Directory.Exists))
            {
                into.Add(new BrokenPathEntry { Directory = missing, Hive = hiveName });
            }
        }
        catch (Exception)
        {
            // Нет доступа (обычно к системной ветке без прав администратора) — пропускаем.
        }
    }
}
