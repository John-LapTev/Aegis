using Aegis.System.Internal;
using Xunit;

namespace Aegis.System.Tests.Internal;

public sealed class UsbIdDatabaseTests
{
    private const string Sample =
        "# comment line ignored\n" +
        "046d  Logitech, Inc.\n" +
        "\tc52b  Unifying Receiver\n" +
        "\tc08b  G502 HERO Gaming Mouse\n" +
        "1532  Razer USA, Ltd\n" +
        "\t0084  Basilisk\n" +
        "C 00  Base class section\n" +     // не производитель → дальнейшие табы не привязываются
        "\t01  must be ignored\n";

    [Fact]
    public void Parse_Vendor_LookupCaseInsensitive()
    {
        var db = UsbIdDatabase.Parse(Sample);

        Assert.Equal("Logitech, Inc.", db.VendorName("046d"));
        Assert.Equal("Logitech, Inc.", db.VendorName("046D"));
        Assert.Equal("Razer USA, Ltd", db.VendorName("1532"));
        Assert.Null(db.VendorName("ffff"));
    }

    [Fact]
    public void Parse_Product_ByVidPid()
    {
        var db = UsbIdDatabase.Parse(Sample);

        Assert.Equal("G502 HERO Gaming Mouse", db.ProductName("046d", "c08b"));
        Assert.Equal("Unifying Receiver", db.ProductName("046D", "C52B"));
        Assert.Null(db.ProductName("046d", "ffff"));
    }

    [Fact]
    public void Parse_ClassSection_DoesNotLeakIntoProducts()
    {
        // После строки "C 00 ..." вложенные табы не должны попасть ни в один VID.
        var db = UsbIdDatabase.Parse(Sample);
        Assert.Null(db.ProductName("1532", "01"));
    }

    [Fact]
    public void EmbeddedDatabase_LoadsThousandsOfVendors() =>
        // Встроенный usb.ids реально загрузился (а не пустой).
        Assert.True(UsbIdDatabase.Instance.VendorCount > 1000);
}
