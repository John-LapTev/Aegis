using System.Net;
using System.Text.Json;
using Aegis.Core.Abstractions;
using Aegis.Core.Models;

namespace Aegis.Threats.VirusTotal;

/// <summary>
/// Клиент VirusTotal API v3: сверяет файл по SHA-256 с множеством антивирусных движков (ADR 0003).
/// HttpClient внедряется снаружи (тестируется фейковым обработчиком). Лимиты бесплатного тарифа
/// (≈4 запроса/мин, 500/день) — учитывать кэшированием хэшей на стороне вызывающего.
/// </summary>
public sealed class VirusTotalClient : IThreatReputationService
{
    private const string FilesEndpoint = "https://www.virustotal.com/api/v3/files/";

    private readonly HttpClient _httpClient;
    private readonly string _apiKey;

    public VirusTotalClient(HttpClient httpClient, string apiKey)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentException.ThrowIfNullOrWhiteSpace(apiKey);
        _httpClient = httpClient;
        _apiKey = apiKey;
    }

    public async Task<FileReputation> CheckHashAsync(string sha256, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sha256);

        using var request = new HttpRequestMessage(HttpMethod.Get, FilesEndpoint + sha256);
        request.Headers.Add("x-apikey", _apiKey);

        using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);

        // 404 — файла нет в базе VirusTotal: нет данных, не считаем чистым.
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return FileReputation.NotFound(sha256);
        }

        // 429 — превышен лимит запросов: не ошибка, а «нет данных сейчас» (воронка покажет это).
        if (response.StatusCode == HttpStatusCode.TooManyRequests)
        {
            return FileReputation.RateLimited(sha256);
        }

        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var document = await JsonDocument.ParseAsync(stream, default, cancellationToken).ConfigureAwait(false);

        var stats = document.RootElement
            .GetProperty("data")
            .GetProperty("attributes")
            .GetProperty("last_analysis_stats");

        var malicious = ReadInt(stats, "malicious");
        var suspicious = ReadInt(stats, "suspicious");
        var total = malicious + suspicious
            + ReadInt(stats, "harmless") + ReadInt(stats, "undetected") + ReadInt(stats, "timeout");

        return new FileReputation
        {
            Hash = sha256,
            Verdict = ReputationMapper.FromStats(malicious, suspicious),
            MaliciousCount = malicious,
            TotalEngines = total,
        };
    }

    private static int ReadInt(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.TryGetInt32(out var number) ? number : 0;
}
