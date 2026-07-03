using System.Collections.Generic;
using System.Threading.Tasks;
using Aegis.App.Views;
using Aegis.Core.Abstractions;
using Aegis.Core.Models;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;

namespace Aegis.App.Services;

/// <summary>Показывает окно остатков (модально, поверх главного окна) и возвращает выбранные пользователем остатки.</summary>
public sealed class LeftoverPrompt : ILeftoverPrompt
{
    public async Task<IReadOnlyList<LeftoverItem>> ConfirmAsync(
        string programName, IReadOnlyList<LeftoverItem> found, bool fullyRemoved = true)
    {
        if (found.Count == 0)
        {
            return found;
        }

        return await Dispatcher.UIThread.InvokeAsync(async () =>
        {
            var owner = (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow;
            if (owner is null)
            {
                return (IReadOnlyList<LeftoverItem>)new List<LeftoverItem>(); // окна нет — на всякий случай ничего не трогаем
            }

            var window = new LeftoverConfirmWindow(programName, found, fullyRemoved);
            return await window.ShowDialog<IReadOnlyList<LeftoverItem>>(owner) ?? new List<LeftoverItem>();
        });
    }
}
