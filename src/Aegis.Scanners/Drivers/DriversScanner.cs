using Aegis.Core.Abstractions;
using Aegis.Core.Models;
using Aegis.Scanners.Probing;

namespace Aegis.Scanners.Drivers;

/// <summary>
/// Сканер драйверов и оборудования (группа <see cref="ScanGroup.Drivers"/>): модель ПК, устройства без
/// драйвера / с проблемой (тачпад, Wi-Fi, звук…), видеокарты. Тексты — простыми словами с вердиктом.
/// «Найти драйвер» запускает официальный поиск через Windows (правка с типом <c>driver-search</c>).
/// </summary>
public sealed class DriversScanner : IScanner
{
    // Сколько устройств максимум проверяем в сети за один скан и сколько запросов параллельно. Держим НИЗКО:
    // бесплатный поиск (DuckDuckGo) при пачке запросов отдаёт «anomaly» (антибот) — мягко пропускаем (без версии).
    private const int MaxLookups = 16;
    private const int LookupConcurrency = 3;

    private readonly IDriverProbe _probe;
    private readonly INvidiaDriverCheck _nvidiaCheck;
    private readonly IDeviceUpdateLookup _lookup;

    public DriversScanner(IDriverProbe probe, INvidiaDriverCheck nvidiaCheck, IDeviceUpdateLookup lookup)
    {
        ArgumentNullException.ThrowIfNull(probe);
        ArgumentNullException.ThrowIfNull(nvidiaCheck);
        ArgumentNullException.ThrowIfNull(lookup);
        _probe = probe;
        _nvidiaCheck = nvidiaCheck;
        _lookup = lookup;
    }

    public ScanGroup Group => ScanGroup.Drivers;

    public async Task<ScanResult> ScanAsync(CancellationToken cancellationToken = default)
    {
        var snapshot = await _probe.ReadAsync(cancellationToken).ConfigureAwait(false);
        var findings = new List<Finding>();

        var model = $"{snapshot.Manufacturer} {snapshot.Model}".Trim();
        if (!string.IsNullOrWhiteSpace(model))
        {
            findings.Add(new Finding
            {
                Id = "driver-model",
                Group = ScanGroup.Drivers,
                Severity = Severity.Ok,
                Title = $"Ваш компьютер: {model}",
                Detail = "Модель определена",
                Explain = "Это марка и модель твоего компьютера. По ней понятно, какие драйверы могут понадобиться. " +
                          "Ничего делать не нужно — просто информация.",
            });
        }

        // Ищем обновления ТОЛЬКО для того, что показываем встроенно: проблемные устройства и видеокарты (не-NVIDIA,
        // у NVIDIA своя точная проверка). Установленные драйверы категорий пачкой НЕ ищем — это тратило до 16
        // веб-запросов впустую (результат в карточке категории не использовался); версию по конкретному драйверу
        // пользователь проверяет по кнопке «Спросить AI» (поиск на месте). Версия из выдачи приблизительная.
        var lookupNames = snapshot.ProblemDevices.Select(d => d.Name)
            .Concat(snapshot.GraphicsCards.Where(g => !IsNvidiaGpu(g.Name)).Select(g => g.Name));
        var updates = await LookupManyAsync(lookupNames, cancellationToken).ConfigureAwait(false);

        findings.AddRange(snapshot.ProblemDevices.Select(d => CreateDeviceFinding(d, Update(updates, d.Name))));
        findings.AddRange(snapshot.DisabledDevices.Select(CreateDisabledFinding));

        foreach (var gpu in snapshot.GraphicsCards)
        {
            findings.Add(await CreateGpuFinding(gpu, Update(updates, gpu.Name), cancellationToken).ConfigureAwait(false));
        }

        // Разносим драйверы по под-разделам (видео/сеть/звук/ввод…) — один раскрывающийся пункт на категорию.
        findings.AddRange(snapshot.InstalledDrivers
            .GroupBy(d => d.Category, StringComparer.OrdinalIgnoreCase)
            .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase)
            .Select(CreateDriverCategoryFinding));

        return new ScanResult { Group = ScanGroup.Drivers, Findings = findings };
    }

    /// <summary>Достаёт результат поиска для устройства из словаря (или Empty, если не искали/не нашли).</summary>
    private static DeviceUpdateResult Update(IReadOnlyDictionary<string, DeviceUpdateResult> updates, string name) =>
        updates.GetValueOrDefault(name) ?? DeviceUpdateResult.Empty;

    /// <summary>Ищет обновления для набора устройств в сети параллельно, с лимитом числа и одновременности.</summary>
    private async Task<IReadOnlyDictionary<string, DeviceUpdateResult>> LookupManyAsync(
        IEnumerable<string> names, CancellationToken cancellationToken)
    {
        var distinct = names
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(MaxLookups)
            .ToList();

        using var gate = new SemaphoreSlim(LookupConcurrency);
        var tasks = distinct.Select(async name =>
        {
            await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                return (name, result: await _lookup.LookupAsync(name, DeviceLookupKind.Driver, cancellationToken).ConfigureAwait(false));
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                return (name, result: DeviceUpdateResult.Empty); // сбой поиска одного устройства не валит весь скан
            }
            finally
            {
                gate.Release();
            }
        });

        var results = await Task.WhenAll(tasks).ConfigureAwait(false);
        return results.ToDictionary(r => r.name, r => r.result, StringComparer.OrdinalIgnoreCase);
    }

    private static bool IsNvidiaGpu(string name)
    {
        var n = name.ToLowerInvariant();
        return n.Contains("nvidia") || n.Contains("geforce") || n.Contains("rtx") || n.Contains("gtx");
    }

    /// <summary>Системные/типовые драйверы Windows — обновление у вендора им не нужно, в сети не ищем.</summary>
    private static bool IsGenericDevice(string name)
    {
        var n = name.ToLowerInvariant();
        return n.StartsWith("microsoft", StringComparison.Ordinal)
               || n.Contains("standard") || n.Contains("generic") || n.Contains("composite")
               || n.Contains("root hub") || n.Contains("hid-compliant") || n.Contains("base system")
               || n.Contains("acpi") || n.Contains("(standard");
    }

    private static Finding CreateDriverCategoryFinding(IGrouping<string, DriverInfo> group)
    {
        var lines = group
            .OrderBy(d => d.DeviceName, StringComparer.OrdinalIgnoreCase)
            .Select(d =>
            {
                var version = d.Version is not null ? $"версия {d.Version}" : "версия неизвестна";
                var date = d.Date is not null ? $", от {d.Date}" : string.Empty;
                // Текст для показа + \u001F + PNPDeviceID (для перезагрузки/переустановки драйвера); VM разбирает по \u001F.
                return $"{d.DeviceName} — {version}{date}\u001F{d.DeviceId}";
            });

        return new Finding
        {
            Id = $"driver-cat-{Sanitize(group.Key)}",
            Group = ScanGroup.Drivers,
            Severity = Severity.Ok,
            Title = $"Драйверы: {group.Key} ({group.Count()})",
            Detail = "Установлены и работают — обновлять не требуется. Раскрой, чтобы посмотреть версии",
            Explain = "Установленные драйверы этой категории и их версии. Раз всё зелёное — они на месте и работают, " +
                      "специально обновлять не нужно. Обновление имеет смысл, только если с устройством есть проблемы " +
                      "(через Windows Update или сайт производителя). Хочешь проверить, есть ли новее по конкретному " +
                      "устройству — нажми «Спросить AI».",
            Data = new Dictionary<string, string>
            {
                ["kind"] = FindingKinds.DriverList,
                ["items"] = string.Join('', lines),
            },
        };
    }

    private static Finding CreateDisabledFinding(ProblemDevice device) => new()
    {
        Id = $"device-disabled-{Sanitize(device.DeviceId)}",
        Group = ScanGroup.Drivers,
        Severity = Severity.Warning,
        Title = $"Отключено: {device.Name}",
        Detail = device.Name,
        Explain = "Это устройство сейчас отключено (например, микрофон или камера могли отключиться случайно). " +
                  "Если оно тебе нужно — нажми «Включить», и оно снова появится в системе. Если отключил намеренно — " +
                  "пометь «Безопасно». Иногда Windows не даёт включить устройство (например, звук по HDMI без " +
                  "подключённого кабеля) — это нормально, тогда просто оставь как есть.",
        Data = new Dictionary<string, string> { ["kind"] = FindingKinds.DeviceEnable, ["deviceId"] = device.DeviceId },
    };

    private static Finding CreateDeviceFinding(ProblemDevice device, DeviceUpdateResult update)
    {
        var (title, explain) = device.NoDriver
            ? ($"Нет драйвера: {device.Name}",
                "Для этого устройства не установлен драйвер — оно может не работать (например, тачпад, Wi-Fi, " +
                "звук или Bluetooth). Нажми «Найти драйвер» — запустим официальный поиск драйвера средствами Windows. " +
                "Если не найдётся — подскажем страницу производителя.")
            : ($"Проблема с устройством: {device.Name}",
                $"Windows сообщает о проблеме с этим устройством (код {device.ErrorCode}). Часто помогает повторная " +
                "установка драйвера. Нажми «Найти драйвер» — запустим официальный поиск через Windows.");

        var data = new Dictionary<string, string> { ["kind"] = FindingKinds.DriverSearch, ["deviceId"] = device.DeviceId };
        var detail = device.Name;
        if (update.DownloadUrl is not null)
        {
            // Нашли в сети возможную страницу драйвера — добавляем кнопку «Открыть страницу» (без номера версии: он ненадёжный).
            data["url"] = update.DownloadUrl;
            explain += " Также нашли в сети возможную страницу драйвера — кнопка «Открыть страницу».";
        }

        return new Finding
        {
            Id = $"driver-device-{Sanitize(device.DeviceId)}",
            Group = ScanGroup.Drivers,
            Severity = Severity.Warning,
            Title = title,
            Detail = detail,
            Explain = explain,
            Data = data,
        };
    }

    private async Task<Finding> CreateGpuFinding(GraphicsCard gpu, DeviceUpdateResult webUpdate, CancellationToken cancellationToken)
    {
        var data = new Dictionary<string, string>();

        // Best-effort проверка свежего драйвера (NVIDIA). Нет интернета / не NVIDIA / API недоступен → null.
        var update = await _nvidiaCheck.CheckAsync(gpu.Name, gpu.DriverVersion, cancellationToken).ConfigureAwait(false);

        if (update is { IsNewer: true })
        {
            var installed = update.InstalledVersion ?? gpu.DriverVersion ?? "?";
            data["url"] = update.DownloadUrl;       // прямая загрузка официального установщика
            data["driver-update"] = "1";            // кнопка «Скачать драйвер»
            return new Finding
            {
                Id = $"driver-gpu-{Sanitize(gpu.Name)}",
                Group = ScanGroup.Drivers,
                Severity = Severity.Info,
                Title = $"Видеокарта: {gpu.Name}",
                Detail = $"Установлен {installed} · доступна {update.LatestVersion}",
                Explain = $"Для твоей видеокарты вышел более свежий драйвер: установлен {installed}, доступна " +
                          $"{update.LatestVersion}. Кнопка «Скачать драйвер» откроет прямую загрузку официального " +
                          "установщика NVIDIA — скачай и запусти его. Свежий драйвер обычно улучшает игры и стабильность.",
                Data = data,
            };
        }

        // Официальная страница вендора (NVIDIA/AMD/Intel); если вендор не распознан — ссылка из веб-поиска.
        var vendorUrl = GpuDriverUrl(gpu.Name) ?? webUpdate.DownloadUrl;
        if (vendorUrl is not null)
        {
            data["url"] = vendorUrl; // «Обновить драйвер» → официальная страница / найденная в сети
        }

        var detail = update is not null
            ? $"Установлен драйвер {update.InstalledVersion ?? gpu.DriverVersion} (актуальный)"
            : gpu.DriverVersion is not null ? $"Установлен драйвер {gpu.DriverVersion}" : "Видеокарта обнаружена";

        return new Finding
        {
            Id = $"driver-gpu-{Sanitize(gpu.Name)}",
            Group = ScanGroup.Drivers,
            Severity = Severity.Ok,
            Title = $"Видеокарта: {gpu.Name}",
            Detail = detail,
            Explain = "Это твоя видеокарта — она отвечает за изображение и игры. Самые свежие драйверы NVIDIA/AMD/Intel " +
                      "выкладывают на свой сайт (в Windows Update они часто старее). Кнопка «Обновить драйвер» откроет " +
                      "официальную страницу — там последняя версия и загрузка.",
            Data = data.Count > 0 ? data : null,
        };
    }

    private static string Sanitize(string value) =>
        new(value.Where(char.IsLetterOrDigit).Take(40).ToArray());

    /// <summary>Официальная страница драйверов видеокарты по названию (NVIDIA/AMD/Intel); null — вендор не распознан.</summary>
    private static string? GpuDriverUrl(string name)
    {
        var lower = name.ToLowerInvariant();
        if (lower.Contains("nvidia") || lower.Contains("geforce") || lower.Contains("rtx") || lower.Contains("gtx"))
        {
            return "https://www.nvidia.com/Download/index.aspx";
        }

        if (lower.Contains("amd") || lower.Contains("radeon"))
        {
            return "https://www.amd.com/en/support/download/drivers.html";
        }

        return lower.Contains("intel") ? "https://www.intel.com/content/www/us/en/download-center/home.html" : null;
    }
}

