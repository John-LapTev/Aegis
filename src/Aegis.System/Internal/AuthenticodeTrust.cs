using System.Runtime.InteropServices;

namespace Aegis.System.Internal;

/// <summary>
/// Проверка доверия к файлу через WinVerifyTrust — как это делает сам Windows. В отличие от чтения
/// встроенного сертификата, видит и подписи через системный каталог (.cat), которыми подписано
/// большинство системных файлов Windows (winlogon, svchost и т.п.). Только на Windows.
/// </summary>
internal static partial class AuthenticodeTrust
{
    private static readonly Guid GenericVerifyV2 = new("00AAC56B-CD44-11d0-8CC2-00C04FC295EE");

    private const uint WtdUiNone = 2;
    private const uint WtdRevokeNone = 0;
    private const uint WtdChoiceFile = 1;
    private const uint WtdStateActionVerify = 1;
    private const uint WtdStateActionClose = 2;
    private const uint WtdCacheOnlyUrlRetrieval = 0x00001000;

    [StructLayout(LayoutKind.Sequential)]
    private struct WintrustFileInfo
    {
        public uint CbStruct;
        [MarshalAs(UnmanagedType.LPWStr)] public string FilePath;
        public IntPtr HFile;
        public IntPtr KnownSubject;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WintrustData
    {
        public uint CbStruct;
        public IntPtr PolicyCallbackData;
        public IntPtr SipClientData;
        public uint UiChoice;
        public uint RevocationChecks;
        public uint UnionChoice;
        public IntPtr File;
        public uint StateAction;
        public IntPtr StateData;
        public IntPtr UrlReference;
        public uint ProvFlags;
        public uint UiContext;
        public IntPtr SignatureSettings;
    }

    [LibraryImport("wintrust.dll", SetLastError = false)]
    private static partial int WinVerifyTrust(IntPtr hwnd, ref Guid actionId, IntPtr data);

    public static bool IsTrusted(string filePath)
    {
        var fileInfo = new WintrustFileInfo
        {
            CbStruct = (uint)Marshal.SizeOf<WintrustFileInfo>(),
            FilePath = filePath,
            HFile = IntPtr.Zero,
            KnownSubject = IntPtr.Zero,
        };

        var fileInfoPtr = Marshal.AllocHGlobal(Marshal.SizeOf<WintrustFileInfo>());
        var dataPtr = IntPtr.Zero;
        try
        {
            Marshal.StructureToPtr(fileInfo, fileInfoPtr, false);

            var data = new WintrustData
            {
                CbStruct = (uint)Marshal.SizeOf<WintrustData>(),
                UiChoice = WtdUiNone,
                RevocationChecks = WtdRevokeNone,
                UnionChoice = WtdChoiceFile,
                File = fileInfoPtr,
                StateAction = WtdStateActionVerify,
                ProvFlags = WtdCacheOnlyUrlRetrieval,
            };

            dataPtr = Marshal.AllocHGlobal(Marshal.SizeOf<WintrustData>());
            Marshal.StructureToPtr(data, dataPtr, false);

            var action = GenericVerifyV2;
            var result = WinVerifyTrust(IntPtr.Zero, ref action, dataPtr);

            // Закрыть состояние проверки (освобождает выделенные WinVerifyTrust ресурсы).
            var verified = Marshal.PtrToStructure<WintrustData>(dataPtr);
            verified.StateAction = WtdStateActionClose;
            Marshal.StructureToPtr(verified, dataPtr, false);
            WinVerifyTrust(IntPtr.Zero, ref action, dataPtr);

            return result == 0;
        }
        finally
        {
            if (dataPtr != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(dataPtr);
            }

            // DestroyStructure освобождает нативную память под строковое поле FilePath (LPWStr), которое
            // StructureToPtr выделил ОТДЕЛЬНО от блока структуры. Без него FreeHGlobal чистит только блок,
            // а строка утекает на КАЖДЫЙ вызов (сотни за скан процессов) — правка аудита.
            Marshal.DestroyStructure<WintrustFileInfo>(fileInfoPtr);
            Marshal.FreeHGlobal(fileInfoPtr);
        }
    }
}
