using System;
using System.IO;
using System.Threading.Tasks;
using Aegis.Core;
using Aegis.Core.Abstractions;
using Aegis.Core.Models;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Aegis.App.ViewModels;

/// <summary>Одна установленная программа в списке «Удаление программ»: название, издатель, размер + состояние удаления.</summary>
public sealed partial class InstalledProgramViewModel : ObservableObject
{
    private readonly IAiAssistant? _aiAssistant;

    public InstalledProgramViewModel(InstalledProgram program, IAiAssistant? aiAssistant = null)
    {
        Program = program;
        _aiAssistant = aiAssistant;
    }

    public InstalledProgram Program { get; }

    public string Name => Program.Name;

    /// <summary>Издатель и версия одной строкой («Google · 120.0» / «120.0» / пусто).</summary>
    public string SubText
    {
        get
        {
            var publisher = Program.Publisher;
            var version = Program.Version;
            if (!string.IsNullOrWhiteSpace(publisher) && !string.IsNullOrWhiteSpace(version))
            {
                return $"{publisher} · {version}";
            }

            return publisher ?? version ?? string.Empty;
        }
    }

    public bool HasSubText => SubText.Length > 0;

    /// <summary>Размер на диске («350 МБ») или пусто, если реестр не сообщил.</summary>
    public string SizeText => Program.EstimatedSizeBytes > 0 ? HumanSize.Format(Program.EstimatedSizeBytes) : string.Empty;

    public bool HasSize => SizeText.Length > 0;

    public bool CanUninstall => Program.CanUninstall;

    /// <summary>Галочка выбора для массового удаления (правка Ивана 1227).</summary>
    [ObservableProperty]
    private bool _isSelected;

    /// <summary>Список зовёт этот колбэк при смене галочки — чтобы пересчитать «Удалить выбранные (N)».</summary>
    public Action? SelectionChanged { get; set; }

    partial void OnIsSelectedChanged(bool value) => SelectionChanged?.Invoke();

    /// <summary>Идёт удаление именно этой программы (крутилка/блокировка кнопки).</summary>
    [ObservableProperty]
    private bool _isUninstalling;

    /// <summary>Программа удалена в этой сессии — показываем «Удалено», прячем кнопку.</summary>
    [ObservableProperty]
    private bool _isRemoved;

    /// <summary>Значок программы (из DisplayIcon) — подтягивается в фоне; null → показываем заглушку.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasIcon))]
    private Bitmap? _icon;

    public bool HasIcon => Icon is not null;

    /// <summary>Ставит значок из готовых PNG-байтов (вызывать на UI-потоке; извлечение PNG — отдельно, в фоне).</summary>
    public void ApplyIcon(byte[] png)
    {
        if (png.Length == 0)
        {
            return;
        }

        using var stream = new MemoryStream(png);
        Icon = new Bitmap(stream);
    }

    // «Спросить AI» — нейросеть объяснит простыми словами, что это за программа и стоит ли её удалять (правка Ивана 1199).
    public bool CanAskAi => _aiAssistant?.IsConfigured == true;

    [ObservableProperty]
    private bool _isAskingAi;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasAiAnswer))]
    private string _aiAnswer = string.Empty;

    public bool HasAiAnswer => AiAnswer.Length > 0;

    [RelayCommand]
    private void DismissAiAnswer() => AiAnswer = string.Empty;

    [RelayCommand]
    private async Task AskAiAsync()
    {
        if (_aiAssistant is null || IsAskingAi)
        {
            return;
        }

        IsAskingAi = true;
        AiAnswer = string.Empty;
        try
        {
            var about = $"«{Program.Name}»"
                        + (string.IsNullOrWhiteSpace(Program.Publisher) ? string.Empty : $", издатель «{Program.Publisher}»")
                        + (string.IsNullOrWhiteSpace(Program.Version) ? string.Empty : $", версия {Program.Version}");

            var prompt = AiSystemPrompt.Text + "\n" +
                         "Пользователь смотрит список установленных программ и хочет понять, что это за программа, чтобы решить, " +
                         $"удалять её или оставить. Программа: {about}. Объясни простыми словами (для человека, не разбирающегося " +
                         "в компьютерах): что это за программа и для чего она; нужна ли она обычному пользователю; можно ли её " +
                         "безопасно удалить или лучше оставить. Коротко и по делу.";

            var result = await _aiAssistant.AskAsync(prompt, $"{Program.Name} что за программа нужна ли безопасно удалять")
                .ConfigureAwait(true);
            AiAnswer = result.Success
                ? (result.Text ?? string.Empty).Trim()
                : result.Error ?? "ИИ-помощник не ответил.";
        }
        catch (Exception ex)
        {
            AiAnswer = "Не удалось получить ответ ИИ: " + ex.Message;
        }
        finally
        {
            IsAskingAi = false;
        }
    }
}
