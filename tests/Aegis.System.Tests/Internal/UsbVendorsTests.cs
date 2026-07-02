using Aegis.System.Internal;
using Xunit;

namespace Aegis.System.Tests.Internal;

public sealed class UsbVendorsTests
{
    [Theory]
    [InlineData(@"USB\VID_046D&PID_C53F\6&1A2B3C4D&0&1", "Logitech")]
    [InlineData(@"USB\VID_1532&PID_007B", "Razer")]
    [InlineData(@"HID\VID_1B1C&PID_1B5E&MI_00", "Corsair")]
    [InlineData(@"USB\vid_1038&pid_1729", "SteelSeries")] // регистр VID не важен
    public void ResolveVendor_KnownVid_ReturnsVendor(string pnpId, string expected) =>
        Assert.Equal(expected, UsbVendors.ResolveVendor(pnpId));

    [Theory]
    [InlineData(@"USB\VID_FFFF&PID_0001")] // неизвестный вендор
    [InlineData(@"ACPI\PNP0303\4&abc")]    // нет VID вообще
    [InlineData("VID_04")]                  // обрезанный VID
    [InlineData("")]
    [InlineData(null)]
    public void ResolveVendor_UnknownOrMalformed_ReturnsNull(string? pnpId) =>
        Assert.Null(UsbVendors.ResolveVendor(pnpId));
}
