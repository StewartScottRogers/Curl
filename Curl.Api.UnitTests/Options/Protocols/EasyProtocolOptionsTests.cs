using Curl.Api.Options.Protocols;

namespace Curl.Api.UnitTests.Options.Protocols;

[TestClass]
public sealed class EasyProtocolOptionsTests
{
    private static readonly EasyProtocolOptions Defaults = new();

    [TestMethod]
    public void New_EveryProtocolRecord_DefaultsToItsOwnDefaults()
    {
        Assert.AreEqual(new EasyFtpOptions(), Defaults.Ftp);
        Assert.AreEqual(new EasySshOptions(), Defaults.Ssh);
        Assert.AreEqual(new EasySmtpOptions(), Defaults.Smtp);
        Assert.AreEqual(new EasyTftpOptions(), Defaults.Tftp);
        Assert.AreEqual(new EasyTelnetOptions(), Defaults.Telnet);
        Assert.AreEqual(new EasyRtspOptions(), Defaults.Rtsp);
        Assert.AreEqual(new EasyWebSocketOptions(), Defaults.WebSocket);
    }

    public static IEnumerable<object[]> SingleOptionChanges =>
    [
        Change("Ftp", o => o with { Ftp = new EasyFtpOptions { Append = true } }, o => o with { Ftp = Defaults.Ftp }),
        Change("Ssh", o => o with { Ssh = new EasySshOptions { Compression = true } }, o => o with { Ssh = Defaults.Ssh }),
        Change("Smtp", o => o with { Smtp = new EasySmtpOptions { MailFrom = "a@example.com" } }, o => o with { Smtp = Defaults.Smtp }),
        Change("Tftp", o => o with { Tftp = new EasyTftpOptions { BlockSize = 1024 } }, o => o with { Tftp = Defaults.Tftp }),
        Change("Telnet", o => o with { Telnet = new EasyTelnetOptions { TelnetOptions = ["TTYPE=vt100"] } }, o => o with { Telnet = Defaults.Telnet }),
        Change("Rtsp", o => o with { Rtsp = new EasyRtspOptions { Request = RtspRequest.Play } }, o => o with { Rtsp = Defaults.Rtsp }),
        Change("WebSocket", o => o with { WebSocket = new EasyWebSocketOptions { Options = WebSocketOptionBits.RawMode } }, o => o with { WebSocket = Defaults.WebSocket }),
    ];

    [TestMethod]
    [DynamicData(nameof(SingleOptionChanges))]
    public void With_OneProtocol_ChangesOnlyThatProtocol(
        string option,
        Func<EasyProtocolOptions, EasyProtocolOptions> set,
        Func<EasyProtocolOptions, EasyProtocolOptions> restore)
    {
        EasyProtocolOptions changed = set(Defaults);

        Assert.AreNotEqual(Defaults, changed, $"setting {option} left the record unchanged");
        Assert.AreEqual(Defaults, restore(changed), $"setting {option} changed another option");
    }

    [TestMethod]
    public void SingleOptionChanges_CoverEveryProperty()
    {
        int properties = typeof(EasyProtocolOptions).GetProperties().Length;

        Assert.HasCount(properties, SingleOptionChanges);
    }

    private static object[] Change(
        string option,
        Func<EasyProtocolOptions, EasyProtocolOptions> set,
        Func<EasyProtocolOptions, EasyProtocolOptions> restore) => [option, set, restore];
}
