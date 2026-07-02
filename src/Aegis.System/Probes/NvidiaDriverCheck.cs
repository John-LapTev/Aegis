using System.Globalization;
using System.Net.Http;
using System.Text.Json;
using Aegis.Scanners.Probing;
using Aegis.System.Internal;

namespace Aegis.System.Probes;

/// <summary>
/// Узнаёт последнюю версию драйвера NVIDIA через их недокументированный AjaxDriverService (как делают сами утилиты
/// NVIDIA) и сравнивает с установленной. Best-effort: нет интернета / 403 / изменился формат → возвращаем null,
/// тогда в UI остаётся «Обновить драйвер → офсайт». Только NVIDIA (у AMD/Intel такого сервиса нет).
/// </summary>
public sealed class NvidiaDriverCheck : INvidiaDriverCheck
{
    private const string Endpoint =
        "https://gfwsl.geforce.com/services_toolkit/services/com/nvidia/services/AjaxDriverService.php";

    private const string OsIdWindows1011X64 = "57"; // Windows 10/11 64-bit

    private readonly HttpClient _httpClient;

    public NvidiaDriverCheck(HttpClient httpClient)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        _httpClient = httpClient;
    }

    public async Task<DriverUpdate?> CheckAsync(string gpuName, string? installedVersion, CancellationToken cancellationToken = default)
    {
        if (gpuName.IndexOf("nvidia", StringComparison.OrdinalIgnoreCase) < 0
            && gpuName.IndexOf("geforce", StringComparison.OrdinalIgnoreCase) < 0)
        {
            return null; // не NVIDIA
        }

        var pfid = NvidiaGpuData.Instance.FindPfid(gpuName);
        if (pfid is null)
        {
            return null; // модель не в базе соответствий
        }

        try
        {
            var url = $"{Endpoint}?func=DriverManualLookup&pfid={pfid}&osID={OsIdWindows1011X64}&dch=1";
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.TryAddWithoutValidation("User-Agent", "Mozilla/5.0"); // без UA сервис иногда отвечает 403

            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            linked.CancelAfter(TimeSpan.FromSeconds(10));

            using var response = await _httpClient.SendAsync(request, linked.Token).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var json = await response.Content.ReadAsStringAsync(linked.Token).ConfigureAwait(false);
            var (version, downloadUrl) = ParseLatest(json);
            if (version is null || downloadUrl is null)
            {
                return null;
            }

            return new DriverUpdate
            {
                LatestVersion = version,
                DownloadUrl = downloadUrl,
                IsNewer = IsNewer(installedVersion, version),
                InstalledVersion = NvidiaVersion(installedVersion),
            };
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>Из ответа AjaxDriverService достать версию и ссылку: IDS[0].downloadInfo.Version/DownloadURL.</summary>
    public static (string? Version, string? Url) ParseLatest(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            if (!document.RootElement.TryGetProperty("IDS", out var ids) || ids.GetArrayLength() == 0)
            {
                return (null, null);
            }

            var info = ids[0].GetProperty("downloadInfo");
            return (info.GetProperty("Version").GetString(), info.GetProperty("DownloadURL").GetString());
        }
        catch (Exception)
        {
            return (null, null);
        }
    }

    /// <summary>Новее ли последняя версия установленной (Win32 «32.0.15.7652» → формат NVIDIA «576.52» → сравнение).</summary>
    public static bool IsNewer(string? installedWin32, string latest)
    {
        var installed = NvidiaVersion(installedWin32);
        if (installed is null)
        {
            return false; // установленную не определили — не заявляем об обновлении
        }

        return ToNumber(latest) > ToNumber(installed);
    }

    /// <summary>Win32-версия драйвера NVIDIA «32.0.15.7652» → маркетинговая «576.52» (последние 5 цифр).</summary>
    internal static string? NvidiaVersion(string? win32Version)
    {
        if (string.IsNullOrWhiteSpace(win32Version))
        {
            return null;
        }

        var digits = new string(win32Version.Where(char.IsDigit).ToArray());
        if (digits.Length < 5)
        {
            return null;
        }

        var last5 = digits[^5..];
        return $"{last5[..3]}.{last5[3..]}";
    }

    private static double ToNumber(string version) =>
        double.TryParse(version, NumberStyles.Float, CultureInfo.InvariantCulture, out var n) ? n : 0;
}
