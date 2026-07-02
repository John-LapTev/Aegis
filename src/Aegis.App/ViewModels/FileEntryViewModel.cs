using System;
using Aegis.Core;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Aegis.App.ViewModels;

/// <summary>
/// Один элемент в раскрытом списке содержимого большой папки: иконка по типу (видео/фото/PDF…), имя, размер,
/// галочка выбора (по умолчанию снята — чтобы случайно не удалить нужное) и открытие по клику. Удаление
/// выбранного делает родительская находка (в Корзину / навсегда).
/// </summary>
public sealed partial class FileEntryViewModel : ObservableObject
{
    private readonly Action<string>? _onOpen;

    /// <summary>Выбран ли элемент галочкой (по умолчанию НЕ выбран — безопасный дефолт для удаления файлов).</summary>
    [ObservableProperty]
    private bool _isSelected;

    /// <summary>Элемент уже удалён — убираем его из списка.</summary>
    [ObservableProperty]
    private bool _isRemoved;

    public FileEntryViewModel(string name, string path, long sizeBytes, bool isDirectory, Action<string>? onOpen)
    {
        Name = name;
        Path = path;
        SizeText = HumanSize.Format(sizeBytes);
        IsDirectory = isDirectory;
        IconKey = ResolveIcon(name, isDirectory);
        _onOpen = onOpen;
        OpenCommand = new RelayCommand(() => _onOpen?.Invoke(path));
    }

    /// <summary>Имя файла/папки.</summary>
    public string Name { get; }

    /// <summary>Полный путь.</summary>
    public string Path { get; }

    /// <summary>Размер в понятном виде («1,4 ГБ»).</summary>
    public string SizeText { get; }

    /// <summary>Это папка (true) или файл (false).</summary>
    public bool IsDirectory { get; }

    /// <summary>Ключ иконки типа (для <c>IconKeyToGeometryConverter</c>): folder/image/video/audio/pdf/document/archive/code/app/file.</summary>
    public string IconKey { get; }

    /// <summary>Открыть файл (его программой по умолчанию) или папку (в проводнике).</summary>
    public IRelayCommand OpenCommand { get; }

    /// <summary>Тип файла по расширению → ключ иконки. Папка всегда «folder».</summary>
    private static string ResolveIcon(string name, bool isDirectory)
    {
        if (isDirectory)
        {
            return "folder";
        }

        return global::System.IO.Path.GetExtension(name).ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" or ".png" or ".gif" or ".bmp" or ".webp" or ".svg" or ".heic"
                or ".tiff" or ".tif" or ".ico" or ".raw" => "image",
            ".mp4" or ".mkv" or ".avi" or ".mov" or ".wmv" or ".flv" or ".webm" or ".m4v"
                or ".mpg" or ".mpeg" or ".3gp" => "video",
            ".mp3" or ".wav" or ".flac" or ".aac" or ".ogg" or ".m4a" or ".wma" or ".opus" => "audio",
            ".pdf" => "pdf",
            ".doc" or ".docx" or ".txt" or ".rtf" or ".odt" or ".xls" or ".xlsx" or ".ods"
                or ".ppt" or ".pptx" or ".odp" or ".csv" or ".md" or ".epub" => "document",
            ".zip" or ".rar" or ".7z" or ".tar" or ".gz" or ".bz2" or ".xz" or ".iso" or ".cab" => "archive",
            ".js" or ".ts" or ".py" or ".cs" or ".cpp" or ".c" or ".h" or ".java" or ".go" or ".rs"
                or ".rb" or ".php" or ".html" or ".htm" or ".css" or ".json" or ".xml" or ".yml"
                or ".yaml" or ".sh" or ".bat" or ".ps1" or ".sql" => "code",
            ".exe" or ".msi" or ".com" or ".scr" or ".apk" or ".dmg" or ".appimage" or ".deb" => "app",
            _ => "file",
        };
    }
}
