using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Aegis.App.ViewModels;

/// <summary>Одна копия дубликата в раскрывающемся списке: путь + удаление по отдельности.</summary>
public sealed partial class DuplicateCopyViewModel : ObservableObject
{
    private readonly Func<string, Task<bool>> _onDeleteFile;

    [ObservableProperty]
    private bool _isRemoved;

    [ObservableProperty]
    private bool _isDeleting;

    public DuplicateCopyViewModel(string path, Func<string, Task<bool>> onDeleteFile)
    {
        Path = path;
        _onDeleteFile = onDeleteFile;
        DeleteCommand = new AsyncRelayCommand(DeleteAsync);
    }

    public string Path { get; }

    public IAsyncRelayCommand DeleteCommand { get; }

    private async Task DeleteAsync()
    {
        if (IsRemoved || IsDeleting)
        {
            return;
        }

        IsDeleting = true;
        try
        {
            IsRemoved = await _onDeleteFile(Path).ConfigureAwait(true);
        }
        finally
        {
            IsDeleting = false;
        }
    }
}
