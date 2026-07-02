using Aegis.Scanners.Probing;

namespace Aegis.Scanners.Internal;

/// <summary>Происхождение программы/процесса для понятной подписи и пометки «безопасно».</summary>
internal enum ProgramOrigin
{
    /// <summary>Системная программа Windows (подписана Microsoft).</summary>
    Windows,

    /// <summary>Драйвер/утилита железа известного вендора (видеокарта, звук…).</summary>
    HardwareVendor,

    /// <summary>Прочая программа с цифровой подписью (личность подтверждена).</summary>
    OtherSigned,

    /// <summary>Без подтверждённой подписи — происхождение неизвестно.</summary>
    Unknown,
}

/// <summary>
/// Классификация программы по ИЗДАТЕЛЮ из цифровой подписи (имя файла подделать легко, подпись — нет) —
/// чтобы Aegis заранее «знал» процессы Windows и известных вендоров (видеокарта и т.п.) и не пугал ими,
/// показывая понятную подпись происхождения.
/// </summary>
internal static class ProgramCatalog
{
    private static readonly (string[] Publishers, string Label)[] HardwareVendors =
    [
        (["NVIDIA Corporation", "NVIDIA"], "Видеокарта (NVIDIA)"),
        (["Advanced Micro Devices, Inc.", "Advanced Micro Devices Inc.", "AMD"], "Видеокарта/чипсет (AMD)"),
        (["Intel Corporation", "Intel(R) Corporation", "Intel"], "Intel (графика/чипсет)"),
        (["Realtek Semiconductor Corp.", "Realtek"], "Звук/сеть (Realtek)"),
        (["Logitech", "Logitech Inc.", "Logitech, Inc."], "Устройство Logitech"),
    ];

    /// <summary>Происхождение + понятная подпись. Безопасным считаем только при действительной подписи.</summary>
    public static (ProgramOrigin Origin, string Label) Classify(string? publisher, SignatureStatus signature)
    {
        if (signature != SignatureStatus.Signed)
        {
            return (ProgramOrigin.Unknown, "неизвестно");
        }

        if (TrustedPublishers.IsMicrosoft(publisher))
        {
            return (ProgramOrigin.Windows, "Windows");
        }

        if (publisher is not null)
        {
            foreach (var (publishers, label) in HardwareVendors)
            {
                if (publishers.Contains(publisher, StringComparer.OrdinalIgnoreCase))
                {
                    return (ProgramOrigin.HardwareVendor, label);
                }
            }

            return (ProgramOrigin.OtherSigned, publisher);
        }

        return (ProgramOrigin.OtherSigned, "подписана");
    }
}
