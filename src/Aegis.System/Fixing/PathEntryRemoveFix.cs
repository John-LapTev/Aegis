using System.Runtime.InteropServices;
using Microsoft.Win32;
using Aegis.Core.Abstractions;
using Aegis.Core.Models;
using Aegis.Scanners.Internal;
using Aegis.System.Backup;

namespace Aegis.System.Fixing;

/// <summary>
/// Убирает одну запись из переменной <c>Path</c> (списка папок, где Windows ищет программы). Значение
/// перечитывается прямо перед правкой — за время между проверкой и нажатием кнопки его мог изменить
/// установщик другой программы, и запись старого значения затёрла бы его изменения.
///
/// Обратимо: прежнее значение целиком уходит в бэкап реестра. Пустой Path никогда не записывается —
/// это сломало бы запуск программ по всей системе.
/// </summary>
public sealed partial class PathEntryRemoveFix : IFix
{
    private readonly RegistryBackupStore _store;
    private readonly RegistryHive _hive;
    private readonly string _subKey;
    private readonly string _entry;

    public PathEntryRemoveFix(
        RegistryBackupStore store,
        string findingId,
        ScanGroup group,
        RegistryHive hive,
        string subKey,
        string entry)
    {
        ArgumentNullException.ThrowIfNull(store);
        _store = store;
        FindingId = findingId;
        Group = group;
        _hive = hive;
        _subKey = subKey;
        _entry = entry;
    }

    public string FindingId { get; }

    public ScanGroup Group { get; }

    public Task<FixOutcome> ApplyAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var baseKey = RegistryKey.OpenBaseKey(_hive, RegistryView.Default);
            using var key = baseKey.OpenSubKey(_subKey, writable: true);
            if (key is null)
            {
                return Task.FromResult(FixOutcome.Failed("Не удалось открыть настройки переменных среды."));
            }

            var current = key.GetValue("Path", null, RegistryValueOptions.DoNotExpandEnvironmentNames)?.ToString();
            var updated = PathListEditor.Remove(current, _entry);
            if (updated is null)
            {
                return Task.FromResult(FixOutcome.Failed(
                    "Эта папка уже не значится в списке — исправлять нечего."));
            }

            // Бэкап ПЕРЕД правкой: вернуть можно будет весь список целиком.
            var backupId = _store.Backup(_hive, _subKey, "Path", "Чистка переменной Path: " + _entry);

            // Тип значения сохраняем: Path почти всегда REG_EXPAND_SZ (в нём живут %SystemRoot% и подобные).
            var kind = key.GetValueKind("Path");
            key.SetValue("Path", updated, kind == RegistryValueKind.String ? RegistryValueKind.String : RegistryValueKind.ExpandString);

            NotifyEnvironmentChanged();
            return Task.FromResult(FixOutcome.Ok(backupId));
        }
        catch (Exception ex)
        {
            return Task.FromResult(FixOutcome.Failed("Не удалось изменить переменную Path: " + ex.Message));
        }
    }

    /// <summary>
    /// Сообщает уже запущенным программам, что переменные среды изменились. Без этого новые значения увидят
    /// только программы, запущенные после перезагрузки.
    /// </summary>
    private static void NotifyEnvironmentChanged()
    {
        try
        {
            SendMessageTimeout(HwndBroadcast, WmSettingChange, IntPtr.Zero, "Environment",
                SmtoAbortIfHung, 5000, out _);
        }
        catch (Exception)
        {
            // Не критично: значение уже записано, программы подхватят его после перезапуска.
        }
    }

    private const int HwndBroadcast = 0xFFFF;
    private const uint WmSettingChange = 0x001A;
    private const uint SmtoAbortIfHung = 0x0002;

    [LibraryImport("user32.dll", EntryPoint = "SendMessageTimeoutW", StringMarshalling = StringMarshalling.Utf16)]
    private static partial IntPtr SendMessageTimeout(
        int hWnd,
        uint msg,
        IntPtr wParam,
        string lParam,
        uint flags,
        uint timeout,
        out IntPtr result);
}
