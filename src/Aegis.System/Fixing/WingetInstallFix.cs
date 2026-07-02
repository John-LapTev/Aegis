using Aegis.Core.Abstractions;
using Aegis.Core.Models;
using Aegis.System.Internal;

namespace Aegis.System.Fixing;

/// <summary>
/// Тихая установка фирменной утилиты через встроенный установщик Windows (winget): он сам скачивает и
/// ставит пакет без окон и без риска подсунуть не то (неверный пакет просто не найдётся). Установка ПО —
/// добавляющая операция; при желании программу можно удалить обычным способом.
/// </summary>
public sealed class WingetInstallFix : IFix
{
    private readonly string _wingetArgs;

    public WingetInstallFix(string findingId, ScanGroup group, string wingetArgs)
    {
        FindingId = findingId;
        Group = group;
        _wingetArgs = wingetArgs;
    }

    public string FindingId { get; }

    public ScanGroup Group { get; }

    public async Task<FixOutcome> ApplyAsync(CancellationToken cancellationToken = default)
    {
        var args = $"install --silent --accept-package-agreements --accept-source-agreements {_wingetArgs}";
        var exitCode = await ProcessRunner.RunAsync("winget", args, cancellationToken).ConfigureAwait(false);
        return exitCode == 0
            ? FixOutcome.OkWithoutBackup()
            : FixOutcome.Failed(
                $"Не удалось установить через встроенный установщик Windows (код {exitCode}). " +
                "Можно нажать «Открыть страницу» и поставить вручную с официального сайта.");
    }
}
