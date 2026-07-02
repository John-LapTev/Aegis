using Xunit;

namespace Aegis.System.Tests;

/// <summary>
/// Тест, который имеет смысл только на Windows (реестр, Корзина, System Restore). На других ОС —
/// автоматически пропускается (Skip), чтобы сборка/прогон на Linux оставались зелёными, а на
/// Windows-машине Ивана тест реально выполнялся.
/// </summary>
public sealed class WindowsOnlyFactAttribute : FactAttribute
{
    public WindowsOnlyFactAttribute()
    {
        if (!OperatingSystem.IsWindows())
        {
            Skip = "Только Windows: использует реестр/файловые операции Windows.";
        }
    }
}
