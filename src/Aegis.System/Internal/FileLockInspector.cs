using System.Runtime.InteropServices;

namespace Aegis.System.Internal;

/// <summary>
/// Определяет, какие программы держат файл/папку открытыми (Restart Manager API), чтобы при неудачном
/// удалении честно сказать пользователю, какую программу закрыть. Только читает — ничего не меняет.
/// Windows-only (rstrtmgr.dll); на других ОС просто вернёт пустой список.
/// </summary>
internal static class FileLockInspector
{
    private const int RmRebootReasonNone = 0;
    private const int CchRmMaxAppName = 255;
    private const int CchRmMaxSvcName = 63;
    private const int ErrorMoreData = 234;

    [StructLayout(LayoutKind.Sequential)]
    private struct RmUniqueProcess
    {
        public int DwProcessId;
        public global::System.Runtime.InteropServices.ComTypes.FILETIME ProcessStartTime;
    }

    private enum RmAppType
    {
        Unknown = 0,
        MainWindow = 1,
        OtherWindow = 2,
        Service = 3,
        Explorer = 4,
        Console = 5,
        Critical = 1000,
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct RmProcessInfo
    {
        public RmUniqueProcess Process;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = CchRmMaxAppName + 1)]
        public string StrAppName;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = CchRmMaxSvcName + 1)]
        public string StrServiceShortName;

        public RmAppType ApplicationType;
        public uint AppStatus;
        public uint TsSessionId;

        [MarshalAs(UnmanagedType.Bool)]
        public bool BRestartable;
    }

    [DllImport("rstrtmgr.dll", CharSet = CharSet.Unicode)]
    private static extern int RmStartSession(out uint pSessionHandle, int dwSessionFlags, string strSessionKey);

    [DllImport("rstrtmgr.dll")]
    private static extern int RmEndSession(uint pSessionHandle);

    [DllImport("rstrtmgr.dll", CharSet = CharSet.Unicode)]
    private static extern int RmRegisterResources(uint pSessionHandle, uint nFiles, string[] rgsFilenames,
        uint nApplications, RmUniqueProcess[]? rgApplications, uint nServices, string[]? rgsServiceNames);

    [DllImport("rstrtmgr.dll")]
    private static extern int RmGetList(uint dwSessionHandle, out uint pnProcInfoNeeded, ref uint pnProcInfo,
        [In, Out] RmProcessInfo[]? rgAffectedApps, ref uint lpdwRebootReasons);

    /// <summary>Понятные названия программ, держащих файл/папку открытыми (пусто — никто/не определилось).</summary>
    public static IReadOnlyList<string> GetLockingProcessNames(string path)
    {
        var names = new List<string>();

        try
        {
            if (RmStartSession(out var handle, 0, Guid.NewGuid().ToString()) != 0)
            {
                return names;
            }

            try
            {
                string[] resources = [path];
                if (RmRegisterResources(handle, 1, resources, 0, null, 0, null) != 0)
                {
                    return names;
                }

                uint needed = 0;
                uint count = 0;
                uint rebootReasons = RmRebootReasonNone;
                var probe = RmGetList(handle, out needed, ref count, null, ref rebootReasons);
                if (probe == ErrorMoreData && needed > 0)
                {
                    var processInfo = new RmProcessInfo[needed];
                    count = needed;
                    if (RmGetList(handle, out needed, ref count, processInfo, ref rebootReasons) == 0)
                    {
                        for (var i = 0; i < count; i++)
                        {
                            var name = processInfo[i].StrAppName;
                            if (!string.IsNullOrWhiteSpace(name) && !names.Contains(name))
                            {
                                names.Add(name);
                            }
                        }
                    }
                }
            }
            finally
            {
                RmEndSession(handle);
            }
        }
        catch (Exception)
        {
            // Restart Manager недоступен (не Windows / нет dll) — вернём, что есть.
        }

        return names;
    }

    /// <summary>PID процессов, держащих файл/папку открытыми (для точного завершения при «грубом» удалении).</summary>
    public static IReadOnlyList<int> GetLockingProcessIds(string path)
    {
        var ids = new List<int>();

        try
        {
            if (RmStartSession(out var handle, 0, Guid.NewGuid().ToString()) != 0)
            {
                return ids;
            }

            try
            {
                string[] resources = [path];
                if (RmRegisterResources(handle, 1, resources, 0, null, 0, null) != 0)
                {
                    return ids;
                }

                uint needed = 0;
                uint count = 0;
                uint rebootReasons = RmRebootReasonNone;
                var probe = RmGetList(handle, out needed, ref count, null, ref rebootReasons);
                if (probe == ErrorMoreData && needed > 0)
                {
                    var processInfo = new RmProcessInfo[needed];
                    count = needed;
                    if (RmGetList(handle, out needed, ref count, processInfo, ref rebootReasons) == 0)
                    {
                        for (var i = 0; i < count; i++)
                        {
                            var pid = processInfo[i].Process.DwProcessId;
                            if (pid > 0 && !ids.Contains(pid))
                            {
                                ids.Add(pid);
                            }
                        }
                    }
                }
            }
            finally
            {
                RmEndSession(handle);
            }
        }
        catch (Exception)
        {
            // Restart Manager недоступен — вернём, что есть.
        }

        return ids;
    }

    /// <summary>Готовый «хвост» для сообщения об ошибке удаления: кто держит файл (или общий текст).</summary>
    public static string DescribeLockers(string path)
    {
        var lockers = GetLockingProcessNames(path);
        return lockers.Count > 0
            ? $" Его держит открытым: {string.Join(", ", lockers)}. Закрой эту программу и попробуй снова."
            : " Возможно, он занят другой программой — закрой её и попробуй снова.";
    }
}
