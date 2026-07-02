namespace Aegis.Scanners.Probing;

/// <summary>Состояние цифровой подписи исполняемого файла (доверие к источнику).</summary>
public enum SignatureStatus
{
    /// <summary>Не удалось определить.</summary>
    Unknown,

    /// <summary>Подписи нет — источник не подтверждён.</summary>
    Unsigned,

    /// <summary>Файл подписан действительной цифровой подписью.</summary>
    Signed,
}
