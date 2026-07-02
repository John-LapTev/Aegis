using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Aegis.App.ViewModels;

/// <summary>
/// Один путь в списке «что именно очистим»: галочка «чистить ли его» (по умолчанию да) + сам путь как
/// кликабельная ссылка (открыть папку). Снятая галочка исключает путь из очистки.
/// </summary>
public sealed partial class JunkLocationViewModel : ObservableObject
{
    /// <summary>Путь к файлу/папке.</summary>
    public string Path { get; }

    /// <summary>Чистить ли этот путь (галочка). По умолчанию включено.</summary>
    [ObservableProperty]
    private bool _isSelected = true;

    /// <summary>Открыть этот путь в проводнике (клик по ссылке-пути).</summary>
    public IRelayCommand OpenCommand { get; }

    public JunkLocationViewModel(string path, Action<string>? onOpen)
    {
        Path = path;
        OpenCommand = new RelayCommand(() => onOpen?.Invoke(path));
    }
}
