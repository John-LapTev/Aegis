using Aegis.System.Internal;
using Xunit;

namespace Aegis.System.Tests;

/// <summary>Парсеры встроенных каталогов (pci.ids, LOLDrivers) + что встроенные ресурсы реально грузятся.</summary>
public sealed class CatalogDatabaseTests
{
    [Fact]
    public void PciIds_Parse_ReadsVendorAndDevice()
    {
        // Формат pci.ids: "vvvv  name", затем "\tdddd  name".
        var db = PciIdDatabase.Parse("10de  NVIDIA Corporation\n\t2484  GA104 [GeForce RTX 3070]\n8086  Intel Corporation\n");

        Assert.Equal("NVIDIA Corporation", db.VendorName("10de"));
        Assert.Equal("GA104 [GeForce RTX 3070]", db.DeviceName("10de", "2484"));
        Assert.Equal("Intel Corporation", db.VendorName("8086"));
        Assert.Null(db.DeviceName("10de", "ffff"));
    }

    [Fact]
    public void PciIds_EmbeddedResource_Loads()
    {
        // Встроенный pci.ids должен распарситься в тысячи вендоров.
        Assert.True(PciIdDatabase.Instance.VendorCount > 1000);
        Assert.Equal("NVIDIA Corporation", PciIdDatabase.Instance.VendorName("10de"));
    }

    [Theory]
    [InlineData("M", true)]
    [InlineData("V", false)]
    public void LolDrivers_Parse_ReadsHashSeverityName(string flag, bool expectedMalicious)
    {
        var sha = new string('a', 64);
        var db = LolDriversDatabase.Parse($"{sha}\t{flag}\tevil.sys\n");

        var entry = db.Lookup(sha);
        Assert.NotNull(entry);
        Assert.Equal(expectedMalicious, entry!.Value.Malicious);
        Assert.Equal("evil.sys", entry.Value.Name);
        Assert.Null(db.Lookup(new string('b', 64))); // неизвестный хэш — безопасен
    }

    [Fact]
    public void LolDrivers_EmbeddedResource_Loads()
    {
        // Встроенный компактный список опасных драйверов должен содержать сотни записей.
        Assert.True(LolDriversDatabase.Instance.Count > 500);
    }
}
