using CommunityToolkit.Mvvm.ComponentModel;

namespace Aegis.App.ViewModels;

/// <summary>
/// Строка в раскрытом списке драйверов: текст (имя + версия) + PNPDeviceID + галочка выбора. По галочкам пользователь
/// выбирает, какие драйверы перезагрузить/переустановить (правка 930). Без DeviceID действовать нельзя — галочка выключена.
/// </summary>
public sealed partial class DriverEntryViewModel : ObservableObject
{
    public DriverEntryViewModel(string displayText, string? deviceId)
    {
        DisplayText = displayText;
        DeviceId = deviceId;
        CanAct = !string.IsNullOrWhiteSpace(deviceId);
        _isSelected = CanAct; // по умолчанию выбраны те, с которыми можно что-то сделать
    }

    /// <summary>Текст для показа: «Имя устройства — версия X, от Y».</summary>
    public string DisplayText { get; }

    /// <summary>PNPDeviceID для pnputil (или null, если неизвестен — тогда действия недоступны).</summary>
    public string? DeviceId { get; }

    /// <summary>Есть ли DeviceID — можно ли перезагружать/переустанавливать этот драйвер.</summary>
    public bool CanAct { get; }

    /// <summary>Выбран ли драйвер галочкой (по умолчанию да, если CanAct).</summary>
    [ObservableProperty]
    private bool _isSelected;
}
