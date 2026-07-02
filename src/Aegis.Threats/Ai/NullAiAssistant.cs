using Aegis.Core.Abstractions;
using Aegis.Core.Models;

namespace Aegis.Threats.Ai;

/// <summary>Заглушка ИИ-помощника, когда ключ не задан: <see cref="IsConfigured"/> = false, UI с ИИ не показывается.</summary>
public sealed class NullAiAssistant : IAiAssistant
{
    public string Name => "—";

    public bool IsConfigured => false;

    public Task<AiResult> AskAsync(string prompt, string? webQuery = null, CancellationToken cancellationToken = default) =>
        Task.FromResult(AiResult.Fail("ИИ-помощник не настроен."));
}
