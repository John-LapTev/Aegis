namespace Aegis.Core.Models;

/// <summary>
/// КЛЮЧИ словаря <see cref="Finding.Data"/> — один реестр вместо «магических строк», разбросанных по сканерам,
/// фабрике исправлений и ViewModel. Раньше производитель (сканер) и потребитель (фабрика/VM) договаривались
/// нетипизированными строками: опечатка молча ломала починку/отображение, компилятор не ловил. Теперь — один
/// источник. Значения ключа <c>Kind</c> — в <see cref="FindingKinds"/>.
/// </summary>
public static class FindingDataKeys
{
    /// <summary>Тип находки — связывает сканер и фабрику исправлений (значения — в <see cref="FindingKinds"/>).</summary>
    public const string Kind = "kind";

    /// <summary>Подсекция внутри вкладки (заголовок группы находок).</summary>
    public const string Section = "section";

    // --- Пути/цели ---
    public const string Path = "path";
    public const string Paths = "paths";
    public const string Exe = "exe";
    public const string File = "file";
    public const string Folder = "folder";
    public const string Url = "url";

    // --- Реестр/служба/задача ---
    public const string Hive = "hive";
    public const string Subkey = "subkey";
    public const string Service = "service";
    public const string Task = "task";

    // --- Устройства/процессы ---
    public const string DeviceId = "deviceId";
    public const string Pid = "pid";
    public const string Publisher = "publisher";
    public const string Reinstall = "reinstall";

    // --- Размер/метрики/бейджи ---
    public const string Bytes = "bytes";
    public const string Info = "info";
    public const string NoBatch = "noBatch";
    public const string Placeholder = "placeholder";
    public const string HealthIcon = "healthIcon";
    public const string Metric = "metric";
    public const string MetricLabel = "metricLabel";
    public const string Category = "category";
    public const string Hint = "hint";
    public const string Raw = "raw";
    public const string FillPercent = "fillPercent";
    public const string FillSeverity = "fillSeverity";
    public const string Letter = "letter";
    public const string Wear = "wear";
    public const string Name = "name";
    public const string Model = "model";
    public const string Items = "items";
    public const string Done = "done";
}
