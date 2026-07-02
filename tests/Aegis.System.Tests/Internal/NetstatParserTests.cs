using Aegis.System.Internal;
using Xunit;

namespace Aegis.System.Tests.Internal;

/// <summary>
/// Регресс на «которая программа подключается»: PID владельца берём из <c>netstat -ano</c>, потому что
/// managed-API его не отдаёт. Парсер должен быть локаль-устойчивым (Windows печатает слово состояния
/// на языке системы) — опираемся только на «TCP» и числовой PID. Чистая логика — на любой ОС.
/// </summary>
public sealed class NetstatParserTests
{
    private const string EnglishSample = """
        Active Connections

          Proto  Local Address          Foreign Address        State           PID
          TCP    192.168.1.5:52345      140.82.121.4:443       ESTABLISHED     4321
          TCP    127.0.0.1:49670        127.0.0.1:49671        ESTABLISHED     1000
          UDP    0.0.0.0:5353           *:*                                    2200
        """;

    // На русской Windows состояние локализовано («УСТАНОВЛЕНО»), но «TCP» и PID — нет.
    private const string RussianSample = """
          Proto  Локальный адрес        Внешний адрес          Состояние       PID
          TCP    192.168.1.5:52345      140.82.121.4:443       УСТАНОВЛЕНО      4321
        """;

    [Fact]
    public void ParseTcpPidMap_MapsLocalEndpointToPid()
    {
        var map = NetstatParser.ParseTcpPidMap(EnglishSample);

        // Ключ — ЛОКАЛЬНАЯ точка (уникальна на сокет), не удалённая (иначе схлопывание → не тот PID).
        Assert.Equal(4321, map["192.168.1.5:52345"]);
        Assert.Equal(1000, map["127.0.0.1:49670"]);
    }

    [Fact]
    public void ParseTcpPidMap_IgnoresUdpAndHeaderRows()
    {
        var map = NetstatParser.ParseTcpPidMap(EnglishSample);

        Assert.Equal(2, map.Count); // только две TCP-строки, без UDP/заголовка
    }

    [Fact]
    public void ParseTcpPidMap_IsLocaleRobust_ParsesLocalizedStateRows()
    {
        var map = NetstatParser.ParseTcpPidMap(RussianSample);

        Assert.Equal(4321, map["192.168.1.5:52345"]);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("какой-то мусор\nбез TCP-строк")]
    public void ParseTcpPidMap_NoTcpRows_ReturnsEmpty(string input) =>
        Assert.Empty(NetstatParser.ParseTcpPidMap(input));
}
