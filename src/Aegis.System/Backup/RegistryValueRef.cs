using Microsoft.Win32;

namespace Aegis.System.Backup;

/// <summary>Координаты значения реестра (куст + путь + имя) — адрес для бэкапа и правки.</summary>
public sealed record RegistryValueRef(RegistryHive Hive, string SubKey, string ValueName);
