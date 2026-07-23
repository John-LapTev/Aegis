using Aegis.Core.Abstractions;
using Aegis.Core.Models;
using Aegis.Scanners.Probing;

namespace Aegis.Scanners.Settings;

/// <summary>
/// Постоянные улучшения для игр (группа <see cref="ScanGroup.Settings"/>): аппаратное планирование
/// видеокарты и библиотеки, без которых игры не запускаются.
///
/// В отличие от игрового режима (временного), это разовые настройки. Каждая предлагается только тогда,
/// когда она действительно применима: включать аппаратное планирование на встроенной видеокарте, в
/// виртуальной машине или на старом драйвере — бесполезно или вредно. Проверки-«гейты» подсмотрены в
/// Sophia Script, где то же делается перед записью значения.
/// </summary>
public sealed class GameTweaksScanner : IScanner
{
    /// <summary>Аппаратное планирование появилось в драйверной модели WDDM 2.7 — на более старых его нет.</summary>
    internal const int MinimumWddmForScheduling = 2700;

    private readonly IGameReadinessProbe _probe;

    public GameTweaksScanner(IGameReadinessProbe probe)
    {
        ArgumentNullException.ThrowIfNull(probe);
        _probe = probe;
    }

    public ScanGroup Group => ScanGroup.Settings;

    public async Task<ScanResult> ScanAsync(CancellationToken cancellationToken = default)
    {
        var readiness = await _probe.ReadAsync(cancellationToken).ConfigureAwait(false);
        var findings = new List<Finding>();

        if (ShouldOfferHardwareScheduling(readiness))
        {
            findings.Add(HardwareSchedulingFinding());
        }

        findings.AddRange(MissingRuntimeFindings(readiness));

        return new ScanResult { Group = ScanGroup.Settings, Findings = findings };
    }

    /// <summary>
    /// Предлагать ли аппаратное планирование: только при отдельной видеокарте, на настоящем компьютере
    /// (не виртуальной машине) и с достаточно свежим драйвером. Чистая логика — проверяется тестами.
    /// </summary>
    internal static bool ShouldOfferHardwareScheduling(GameReadiness readiness) =>
        !readiness.HardwareSchedulingEnabled
        && readiness.HasDiscreteGpu
        && !readiness.IsVirtualMachine
        && readiness.WddmVersion >= MinimumWddmForScheduling;

    private static Finding HardwareSchedulingFinding() => new()
    {
        Id = "game-hags",
        Group = ScanGroup.Settings,
        Severity = Severity.Info,
        Title = "Можно включить аппаратное планирование видеокарты",
        Detail = "видеокарта поддерживает, сейчас выключено",
        Explain = "Обычно очередью задач для видеокарты управляет процессор. Аппаратное планирование передаёт " +
                  "эту работу самой видеокарте: в играх это немного снижает задержку и разгружает процессор. " +
                  "Твоя видеокарта и драйвер это поддерживают. Изменение вступит в силу после перезагрузки и " +
                  "полностью обратимо — если игры вдруг начнут вести себя хуже, нажми «Вернуть» в разделе «Бэкапы».",
        Data = new Dictionary<string, string>
        {
            [FindingDataKeys.Kind] = FindingKinds.RegistryToggle,
            [FindingDataKeys.Hive] = "HKLM",
            [FindingDataKeys.Subkey] = @"SYSTEM\CurrentControlSet\Control\GraphicsDrivers",
            [FindingDataKeys.Name] = "HwSchMode",
            ["value"] = "2",
            [FindingDataKeys.Section] = "Для игр",
        },
    };

    /// <summary>Библиотеки, без которых игры вылетают на старте с непонятной ошибкой.</summary>
    private static IEnumerable<Finding> MissingRuntimeFindings(GameReadiness readiness)
    {
        if (!readiness.HasVisualCppX64)
        {
            yield return RuntimeFinding(
                "game-vcredist-x64",
                "Не установлен пакет Visual C++ (64-разрядный)",
                "Большинство современных игр и программ собраны так, что без этого набора библиотек от Microsoft " +
                "они просто не запускаются — выдают ошибку вроде «отсутствует VCRUNTIME140.dll» или молча " +
                "закрываются. Пакет официальный, бесплатный и весит немного. Установим его.",
                "--id Microsoft.VCRedist.2015+.x64 --exact");
        }

        if (!readiness.HasVisualCppX86)
        {
            yield return RuntimeFinding(
                "game-vcredist-x86",
                "Не установлен пакет Visual C++ (32-разрядный)",
                "Тот же набор библиотек Microsoft, но для программ постарше — многие игры прошлых лет без него " +
                "не запускаются. Ставится рядом с 64-разрядным и не мешает ему.",
                "--id Microsoft.VCRedist.2015+.x86 --exact");
        }

        if (!readiness.HasDirectXRuntime)
        {
            yield return RuntimeFinding(
                "game-directx",
                "Не установлены библиотеки DirectX для игр",
                "В Windows встроен современный DirectX, но многим играм нужны и старые его части (обычно ошибка " +
                "про «d3dx9_43.dll» или «XINPUT1_3.dll»). Официальный набор от Microsoft добавит их, ничего не " +
                "заменяя и не ломая.",
                "--id Microsoft.DirectX --exact");
        }
    }

    private static Finding RuntimeFinding(string id, string title, string explain, string wingetId) => new()
    {
        Id = id,
        Group = ScanGroup.Settings,
        Severity = Severity.Info,
        Title = title,
        Detail = "нужно для запуска игр",
        Explain = explain,
        Data = new Dictionary<string, string>
        {
            [FindingDataKeys.Kind] = FindingKinds.WingetInstall,
            ["winget"] = wingetId,
            [FindingDataKeys.Section] = "Для игр",
        },
    };
}
