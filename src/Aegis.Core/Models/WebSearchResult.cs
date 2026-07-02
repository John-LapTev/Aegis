namespace Aegis.Core.Models;

/// <summary>Один результат веб-поиска (заголовок, ссылка, краткое описание) — для подмешивания в ответ ИИ.</summary>
public sealed record WebSearchResult
{
    public required string Title { get; init; }

    public required string Url { get; init; }

    public string Snippet { get; init; } = string.Empty;
}
