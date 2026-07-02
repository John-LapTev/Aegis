using System.Diagnostics;
using Aegis.Core.Abstractions;
using Aegis.Core.Models;

namespace Aegis.System.Fixing;

/// <summary>
/// Перезагружает компьютер (кнопка «Перезагрузить» в находке «Нужна перезагрузка»). Задержка 60 секунд —
/// Windows покажет предупреждение, есть время сохранить файлы и при желании отменить (shutdown /a).
/// Если ранее применялись рискованные правки, после перезапуска появится окно проверки «всё работает?»
/// (его ставит планировщик отката), и при неподтверждении произойдёт авто-откат.
/// </summary>
public sealed class RebootFix : IFix
{
    public RebootFix(string findingId) => FindingId = findingId;

    public string FindingId { get; }

    public ScanGroup Group => ScanGroup.System;

    // Перезагрузка обратима по своей сути — «зонтичная» точка восстановления не нужна (и зря тормозит ~25с).
    public bool RequiresSystemRestorePoint => false;

    public Task<FixOutcome> ApplyAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "shutdown",
                Arguments = "/r /t 60 /c \"Aegis: перезагрузка через минуту. Сохраните файлы. " +
                            "После запуска может появиться окно проверки — подтвердите, что всё работает.\"",
                UseShellExecute = false,
                CreateNoWindow = true,
            });
            return Task.FromResult(FixOutcome.OkWithoutBackup());
        }
        catch (Exception ex)
        {
            return Task.FromResult(FixOutcome.Failed("Не удалось запустить перезагрузку: " + ex.Message));
        }
    }
}
