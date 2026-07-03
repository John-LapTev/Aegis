using Aegis.System.Optimize;
using Xunit;

namespace Aegis.System.Tests.Optimize;

public sealed class MemoryOptimizerTests
{
    [Theory]
    [InlineData("GoogleUpdate")]
    [InlineData("googleupdate")]   // регистр не важен
    [InlineData("AdobeARM")]
    [InlineData("jusched")]
    [InlineData("YourPhone")]
    public void Classify_KnownBackgroundProcess_ReturnsFriendlyName(string name)
    {
        Assert.NotNull(MemoryOptimizer.Classify(name));
    }

    [Theory]
    [InlineData("chrome")]         // пользовательский браузер — не трогаем
    [InlineData("explorer")]       // системный — не трогаем
    [InlineData("winlogon")]       // критичный — не трогаем
    [InlineData("MyGame")]
    public void Classify_UserOrSystemProcess_ReturnsNull(string name)
    {
        Assert.Null(MemoryOptimizer.Classify(name));
    }
}
