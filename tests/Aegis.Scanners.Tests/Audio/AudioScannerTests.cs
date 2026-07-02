using Aegis.Core.Models;
using Aegis.Scanners.Audio;
using Aegis.Scanners.Probing;
using Xunit;

namespace Aegis.Scanners.Tests.Audio;

public sealed class AudioScannerTests
{
    [Fact]
    public async Task ScanAsync_OnboardAndDisplayAudio_NoEnhancers_ExplainsSetupAndReportsClean()
    {
        var scanner = new AudioScanner(new FakeAudioProbe(new AudioSnapshot
        {
            Devices =
            [
                new AudioDeviceInfo { Name = "Realtek High Definition Audio", Manufacturer = "Realtek" },
                new AudioDeviceInfo { Name = "NVIDIA High Definition Audio", Manufacturer = "NVIDIA" },
            ],
            EnhancementServices = [],
        }));

        var result = await scanner.ScanAsync();

        Assert.Equal(ScanGroup.Drivers, result.Group);
        Assert.Contains(result.Findings, f => f.Id == "audio-setup" && f.Severity == Severity.Ok);
        Assert.Contains(result.Findings, f => f.Id == "audio-enhancers-none" && f.Severity == Severity.Ok);
    }

    [Fact]
    public async Task ScanAsync_EnhancerServicePresent_WarnsWithReversibleDisableFix()
    {
        var scanner = new AudioScanner(new FakeAudioProbe(new AudioSnapshot
        {
            Devices = [new AudioDeviceInfo { Name = "Realtek(R) Audio", Manufacturer = "Realtek" }],
            EnhancementServices = [new AudioServiceInfo { Product = "Nahimic", ServiceName = "NahimicService" }],
        }));

        var result = await scanner.ScanAsync();

        var enhancer = Assert.Single(result.Findings, f => f.Id == "audio-enhancer-Nahimic");
        Assert.Equal(Severity.Warning, enhancer.Severity);
        Assert.NotNull(enhancer.Data);
        Assert.Equal("service-disable", enhancer.Data!["kind"]);
        Assert.Equal("NahimicService", enhancer.Data!["service"]);
        // Без улучшайзеров-«нет» находки, раз один найден.
        Assert.DoesNotContain(result.Findings, f => f.Id == "audio-enhancers-none");
    }

    private sealed class FakeAudioProbe(AudioSnapshot snapshot) : IAudioProbe
    {
        public Task<AudioSnapshot> ReadAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(snapshot);
    }
}
