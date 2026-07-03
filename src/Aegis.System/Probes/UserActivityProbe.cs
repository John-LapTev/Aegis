using System.Runtime.InteropServices;
using Aegis.Core.Abstractions;

namespace Aegis.System.Probes;

/// <summary>Простой пробник простоя пользователя через <c>GetLastInputInfo</c> (Win32).</summary>
public sealed class UserActivityProbe : IUserActivityProbe
{
    public TimeSpan GetIdleDuration()
    {
        try
        {
            var info = new LastInputInfo { Size = (uint)Marshal.SizeOf<LastInputInfo>() };
            if (!GetLastInputInfo(ref info))
            {
                return TimeSpan.Zero;
            }

            // Оба значения — миллисекунды от старта системы; разница переполнения не боится (беззнаковая арифметика).
            var idleMs = unchecked((uint)Environment.TickCount - info.LastInputTick);
            return TimeSpan.FromMilliseconds(idleMs);
        }
        catch (Exception)
        {
            return TimeSpan.Zero; // не смогли определить — считаем «не знаем» (эвристика просто не добавит бонус)
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct LastInputInfo
    {
        public uint Size;
        public uint LastInputTick;
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetLastInputInfo(ref LastInputInfo info);
}
