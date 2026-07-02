namespace Aegis.Scanners.Probing;

/// <summary>
/// Поставщик данных для раздела «Утилиты»: производитель/модель ПК, установленные программы, вендоры
/// периферии, наличие интернета. Только ЧИТАЕТ. Каталог утилит и тексты — в <see cref="Utilities.UtilitiesScanner"/>.
/// </summary>
public interface IUtilitiesProbe
{
    Task<UtilitiesSnapshot> ReadAsync(CancellationToken cancellationToken = default);
}
