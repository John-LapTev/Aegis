using System.Runtime.InteropServices;

namespace Aegis.System.Optimize;

/// <summary>
/// Запрет засыпания и гашения экрана (штатный механизм Windows <c>SetThreadExecutionState</c> — тот же, что
/// используют видеоплееры). Ничего в системе не меняет: как только запрет снят или программа закрыта, Windows
/// снова засыпает по своим настройкам.
/// </summary>
internal static partial class SleepBlocker
{
    [Flags]
    private enum ExecutionState : uint
    {
        SystemRequired = 0x00000001,
        DisplayRequired = 0x00000002,
        Continuous = 0x80000000,
    }

    [LibraryImport("kernel32.dll", SetLastError = true)]
    private static partial uint SetThreadExecutionState(ExecutionState flags);

    /// <summary>Включить (true) или снять (false) запрет засыпания.</summary>
    public static void Set(bool enabled)
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        try
        {
            var state = enabled
                ? ExecutionState.Continuous | ExecutionState.SystemRequired | ExecutionState.DisplayRequired
                : ExecutionState.Continuous;
            SetThreadExecutionState(state);
        }
        catch (Exception)
        {
            // Не критично: без запрета компьютер просто может уснуть по своим настройкам.
        }
    }
}
