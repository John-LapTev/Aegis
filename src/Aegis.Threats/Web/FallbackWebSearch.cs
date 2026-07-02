using Aegis.Core.Abstractions;
using Aegis.Core.Models;

namespace Aegis.Threats.Web;

/// <summary>
/// Веб-поиск с резервом: сначала основной провайдер (Brave, если есть ключ), при пустом результате/сбое —
/// запасной (DuckDuckGo, бесплатный). Так массовый поиск надёжен с ключом и всё равно работает без него.
/// </summary>
public sealed class FallbackWebSearch : IWebSearch
{
    private readonly IWebSearch _primary;
    private readonly IWebSearch _fallback;

    public FallbackWebSearch(IWebSearch primary, IWebSearch fallback)
    {
        _primary = primary;
        _fallback = fallback;
    }

    public async Task<IReadOnlyList<WebSearchResult>> SearchAsync(
        string query, int maxResults = 5, CancellationToken cancellationToken = default)
    {
        var primary = await _primary.SearchAsync(query, maxResults, cancellationToken).ConfigureAwait(false);
        return primary.Count > 0
            ? primary
            : await _fallback.SearchAsync(query, maxResults, cancellationToken).ConfigureAwait(false);
    }
}
