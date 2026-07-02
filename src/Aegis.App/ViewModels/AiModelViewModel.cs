using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Aegis.Core.Models;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Aegis.App.ViewModels;

/// <summary>
/// Одна модель в разделе «Нейросети» (поисковая ИЛИ языковая): место в цепочке + кнопка «Проверить» с
/// лампочкой-индикатором (🟢 работает / 🟡 лимит / 🔴 не отвечает / ⚪ не проверяли) + свой ключ
/// («Заменить»/«Вернуть»). Как проверять модель — задаёт делегат <c>check</c> (поиск или языковой запрос).
/// </summary>
public sealed partial class AiModelViewModel : ObservableObject
{
    private readonly string _keyFilePath;
    private readonly string _getKeyUrl;
    private readonly Action<string> _openUrl;
    private readonly Func<CancellationToken, Task<AiResult>>? _check;
    private bool _checkingStatus;

    public AiModelViewModel(string name, string order, string description, string keyFilePath,
        string getKeyUrl, Action<string> openUrl, Func<CancellationToken, Task<AiResult>>? check = null)
    {
        Name = name;
        Order = order;
        Description = description;
        _keyFilePath = keyFilePath;
        _getKeyUrl = getKeyUrl;
        _openUrl = openUrl;
        _check = check;
        RefreshStatus();
    }

    /// <summary>Текст статуса: «Не проверено» / «Работает» / «Лимит исчерпан» / «Нет доступа».</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasModelStatus))]
    private string _modelStatus = "Не проверено";

    /// <summary>Важность статуса (для цвета текста и лампочки).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LightBrush))]
    private Severity _modelStatusSeverity = Severity.Ok;

    /// <summary>Проверяли ли уже (до проверки лампочка серая).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LightBrush))]
    private bool _modelChecked;

    /// <summary>Нет доступа (для значка «?» с пояснением про регион/VPN).</summary>
    [ObservableProperty]
    private bool _modelNoAccess;

    /// <summary>Под статусом: через сколько сбросится лимит («лимит на сегодня, обновится завтра» / «≈1 мин»).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasModelResetHint))]
    private string _modelResetHint = string.Empty;

    public bool HasModelStatus => !string.IsNullOrEmpty(ModelStatus);

    public bool HasModelResetHint => !string.IsNullOrEmpty(ModelResetHint);

    /// <summary>Цвет «лампочки»: ⚪ серый (не проверяли) / 🟢 / 🟡 / 🔴 (по результату проверки).</summary>
    public IBrush LightBrush => new SolidColorBrush(Color.Parse(
        !ModelChecked ? StatusColors.Neutral
        : ModelStatusSeverity switch
        {
            Severity.Ok => StatusColors.Ok,
            Severity.Warning => StatusColors.Warn,
            Severity.Danger => StatusColors.Danger,
            _ => StatusColors.Neutral,
        }));

    public string NoAccessTooltip =>
        "Не удалось соединиться с этой моделью. Возможно, нет интернета или она недоступна в твоём регионе " +
        "(бывает, нужен VPN). Программа работает и без неё — ответит следующая модель цепочки.";

    /// <summary>Проверить модель ПО КНОПКЕ: один живой запрос → лампочка загорается по результату.</summary>
    [RelayCommand]
    private Task Check() => CheckStatusAsync();

    /// <summary>Живой тест модели (поиск или языковой запрос — задаётся делегатом). Обновляет статус и лампочку.</summary>
    public async Task CheckStatusAsync()
    {
        if (_check is null || _checkingStatus)
        {
            return;
        }

        _checkingStatus = true;
        ModelStatus = "Проверка…";
        ModelStatusSeverity = Severity.Info;
        ModelChecked = false;
        ModelNoAccess = false;
        ModelResetHint = string.Empty;
        try
        {
            var result = await _check(CancellationToken.None).ConfigureAwait(true);
            if (result.Success)
            {
                ModelStatus = "Работает";
                ModelStatusSeverity = Severity.Ok;
            }
            else if (result.LimitReached)
            {
                ModelStatus = "Лимит исчерпан";
                ModelStatusSeverity = Severity.Warning;
                ModelResetHint = result.RetryAfter ?? string.Empty; // через сколько обновится (если API сообщил)
            }
            else
            {
                ModelStatus = "Нет доступа";
                ModelStatusSeverity = Severity.Danger;
                ModelNoAccess = true;
            }
        }
        catch (Exception)
        {
            ModelStatus = "Нет доступа";
            ModelStatusSeverity = Severity.Danger;
            ModelNoAccess = true;
        }
        finally
        {
            ModelChecked = true; // лампочка загорелась цветом результата
            _checkingStatus = false;
        }
    }

    /// <summary>Имя модели (Gemini/Groq/Mistral) — используется и для значка через ModelIcon-конвертеры.</summary>
    public string Name { get; }

    /// <summary>Место в цепочке: «1-я», «2-я»… (приоритет — от лучшей к запасной).</summary>
    public string Order { get; }

    /// <summary>Короткое пояснение про модель простыми словами.</summary>
    public string Description { get; }

    [ObservableProperty]
    private string _keyInput = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasKeyStatus))]
    private string _keyStatus = string.Empty;

    [ObservableProperty]
    private bool _hasOwnKey;

    // Текущий источник ключа НЕ дублируем подписью (об этом сказано в блоке-пояснении сверху раздела).
    // KeyStatus показываем только как отклик на действие (сохранил/вернул). HasOwnKey управляет кнопкой «Вернуть».
    private void RefreshStatus() => HasOwnKey = File.Exists(_keyFilePath);

    /// <summary>Есть ли сообщение-отклик для показа.</summary>
    public bool HasKeyStatus => !string.IsNullOrEmpty(KeyStatus);

    /// <summary>Сохранить свой ключ (заменить общий). Применится после перезапуска программы.</summary>
    [RelayCommand]
    private void Replace()
    {
        var key = KeyInput.Trim();
        if (key.Length < 10)
        {
            KeyStatus = "Введите корректный ключ.";
            return;
        }

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_keyFilePath)!);
            File.WriteAllText(_keyFilePath, key);
            KeyInput = string.Empty;
            HasOwnKey = true;
            KeyStatus = "Ваш ключ сохранён — заработает после перезапуска программы.";
        }
        catch (Exception ex)
        {
            KeyStatus = "Не удалось сохранить: " + ex.Message;
        }
    }

    /// <summary>Удалить свой ключ и вернуть общий. Применится после перезапуска.</summary>
    [RelayCommand]
    private void Restore()
    {
        try
        {
            if (File.Exists(_keyFilePath))
            {
                File.Delete(_keyFilePath);
            }

            HasOwnKey = false;
            KeyStatus = "Возвращён общий ключ — заработает после перезапуска.";
        }
        catch (Exception ex)
        {
            KeyStatus = "Не удалось вернуть: " + ex.Message;
        }
    }

    /// <summary>Открыть страницу, где можно бесплатно получить свой ключ.</summary>
    [RelayCommand]
    private void OpenGetKey() => _openUrl(_getKeyUrl);
}
