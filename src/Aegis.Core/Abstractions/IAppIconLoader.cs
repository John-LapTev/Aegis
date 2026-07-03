namespace Aegis.Core.Abstractions;

/// <summary>Извлекает значок программы (из пути DisplayIcon: exe с индексом или .ico) в PNG-байты для показа в списке.</summary>
public interface IAppIconLoader
{
    /// <summary>PNG-изображение значка по пути (или null, если извлечь не удалось / не Windows).</summary>
    byte[]? LoadPng(string iconPath);
}
