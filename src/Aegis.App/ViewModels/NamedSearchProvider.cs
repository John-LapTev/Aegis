using Aegis.Core.Abstractions;

namespace Aegis.App.ViewModels;

/// <summary>Поисковый провайдер с именем (Tavily/Serper…) — чтобы раздел «Нейросети» мог показать и проверить каждый отдельно.</summary>
public sealed record NamedSearchProvider(string Name, IWebSearch Search);
