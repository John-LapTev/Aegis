using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace Aegis.System.Internal;

/// <summary>
/// Уникальный идентификатор файла на томе (том + индекс записи MFT). Нужен, чтобы «жёсткие ссылки»
/// (несколько путей к одним и тем же данным) не считались дублями — удаление такой «копии» места не освобождает.
/// Windows-only; на других ОС вернёт null.
/// </summary>
internal static class FileIdentity
{
    [StructLayout(LayoutKind.Sequential)]
    private struct ByHandleFileInformation
    {
        public uint FileAttributes;
        public global::System.Runtime.InteropServices.ComTypes.FILETIME CreationTime;
        public global::System.Runtime.InteropServices.ComTypes.FILETIME LastAccessTime;
        public global::System.Runtime.InteropServices.ComTypes.FILETIME LastWriteTime;
        public uint VolumeSerialNumber;
        public uint FileSizeHigh;
        public uint FileSizeLow;
        public uint NumberOfLinks;
        public uint FileIndexHigh;
        public uint FileIndexLow;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetFileInformationByHandle(SafeFileHandle handle, out ByHandleFileInformation info);

    /// <summary>«том:индекс» файла или null, если определить не удалось (тогда считаем файл уникальным).</summary>
    public static string? TryGet(string path)
    {
        try
        {
            using var handle = File.OpenHandle(path, FileMode.Open, FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete);
            if (GetFileInformationByHandle(handle, out var info))
            {
                return $"{info.VolumeSerialNumber}:{info.FileIndexHigh}:{info.FileIndexLow}";
            }
        }
        catch (Exception)
        {
            // Файл занят/недоступен/не Windows — считаем уникальным.
        }

        return null;
    }
}
