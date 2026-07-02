using System.Runtime.InteropServices;

namespace Aegis.System.Internal;

/// <summary>
/// Удаление файла/папки в Корзину через системный <c>SHFileOperation</c> с флагом
/// <c>FOF_WANTNUKEWARNING</c>: если элемент НЕ помещается в Корзину или Корзина для тома отключена/недоступна
/// (съёмный/сетевой диск, «удалять сразу»), Windows предупреждает пользователя ВМЕСТО тихого безвозвратного
/// удаления. Возвращаем успех только если элемент реально отправлен в Корзину (не отменено, без ошибки) —
/// чтобы никогда не выдавать безвозвратное удаление за «обратимое» (ADR 0002).
/// </summary>
internal static class ShellFileOperation
{
    private const uint FO_DELETE = 0x0003;
    private const ushort FOF_SILENT = 0x0004;
    private const ushort FOF_NOCONFIRMATION = 0x0010;
    private const ushort FOF_ALLOWUNDO = 0x0040;
    private const ushort FOF_NOERRORUI = 0x0400;
    private const ushort FOF_WANTNUKEWARNING = 0x4000;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct ShFileOpStruct
    {
        public IntPtr Hwnd;
        public uint Func;
        public string From;
        public string? To;
        public ushort Flags;
        [MarshalAs(UnmanagedType.Bool)] public bool AnyOperationsAborted;
        public IntPtr NameMappings;
        public string? ProgressTitle;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int SHFileOperation(ref ShFileOpStruct fileOp);

    /// <summary>
    /// Отправить файл или папку в Корзину. <c>true</c> — элемент реально в Корзине (обратимо);
    /// <c>false</c> — не удалось ИЛИ пользователь отменил предупреждение о безвозвратном удалении
    /// (тогда элемент НЕ тронут / удалять безвозвратно мы не стали).
    /// </summary>
    public static bool RecycleToBin(string path)
    {
        // pFrom должен оканчиваться двойным нулём (список путей).
        var op = new ShFileOpStruct
        {
            Func = FO_DELETE,
            From = path + "\0\0",
            Flags = (ushort)(FOF_ALLOWUNDO | FOF_SILENT | FOF_NOERRORUI | FOF_NOCONFIRMATION | FOF_WANTNUKEWARNING),
        };

        var result = SHFileOperation(ref op);
        return result == 0 && !op.AnyOperationsAborted;
    }

    /// <summary>
    /// Отправить СРАЗУ МНОГО файлов в Корзину одним системным вызовом (pFrom — список путей через \0).
    /// Это в РАЗЫ быстрее, чем по одному файлу (кэш браузера — тысячи мелких файлов). <c>true</c> —
    /// операция прошла без ошибки и не отменена.
    /// </summary>
    public static bool RecycleManyToBin(IReadOnlyList<string> paths)
    {
        if (paths.Count == 0)
        {
            return true;
        }

        // Список путей: каждый оканчивается \0, и весь список — ещё одним \0 (двойной ноль в конце).
        var op = new ShFileOpStruct
        {
            Func = FO_DELETE,
            From = string.Join('\0', paths) + "\0\0",
            Flags = (ushort)(FOF_ALLOWUNDO | FOF_SILENT | FOF_NOERRORUI | FOF_NOCONFIRMATION | FOF_WANTNUKEWARNING),
        };

        var result = SHFileOperation(ref op);
        return result == 0 && !op.AnyOperationsAborted;
    }
}
