using Aegis.System.Internal;
using Xunit;

namespace Aegis.System.Tests.Internal;

/// <summary>Разбор вывода netstat — чистая функция, проверяется на любой ОС.</summary>
public sealed class NetstatListeningPortsTests
{
    private const string Sample = """
          Активные подключения

          Имя    Локальный адрес        Внешний адрес          Состояние       PID
          TCP    0.0.0.0:135            0.0.0.0:0              LISTENING       1024
          TCP    0.0.0.0:3389           0.0.0.0:0              LISTENING       1200
          TCP    127.0.0.1:5354         0.0.0.0:0              LISTENING       3300
          TCP    [::]:445               [::]:0                 LISTENING       4
          TCP    192.168.1.5:52344      93.184.216.34:443      ESTABLISHED     7788
        """;

    [Fact]
    public void ParsesListeningPorts()
    {
        var ports = NetstatParser.ParseListeningPorts(Sample);

        Assert.Contains(135, ports);
        Assert.Contains(3389, ports);
        Assert.Contains(445, ports);
    }

    [Fact]
    public void SkipsLoopbackOnly()
    {
        // 127.0.0.1 снаружи недоступен — это не «открытая дверь», сообщать о нём нельзя.
        Assert.DoesNotContain(5354, NetstatParser.ParseListeningPorts(Sample));
    }

    [Fact]
    public void SkipsEstablishedConnections()
    {
        Assert.DoesNotContain(52344, NetstatParser.ParseListeningPorts(Sample));
    }

    [Fact]
    public void EmptyInput_NoPorts()
    {
        Assert.Empty(NetstatParser.ParseListeningPorts(string.Empty));
    }

    [Fact]
    public void ResultIsSortedAndDeduplicated()
    {
        const string duplicates = """
            TCP    0.0.0.0:8080           0.0.0.0:0              LISTENING       10
            TCP    [::]:8080              [::]:0                 LISTENING       10
            TCP    0.0.0.0:80             0.0.0.0:0              LISTENING       11
            """;

        Assert.Equal([80, 8080], NetstatParser.ParseListeningPorts(duplicates));
    }
}
