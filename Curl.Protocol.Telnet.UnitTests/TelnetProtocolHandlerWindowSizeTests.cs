using Curl.Protocol.Abstractions;
using Curl.Protocol.Telnet.Fakes;

namespace Curl.Protocol.Telnet;

/// <summary>
/// Pins how a <c>telnet://</c> session negotiates NAWS (option 31) and sends the
/// <c>-t WS=COLUMNSxROWS</c> window size against curl 8.21.0, measured on 2026-09-26
/// against a loopback listener (captures in BL-085's Notes).
/// </summary>
[TestClass]
public sealed class TelnetProtocolHandlerWindowSizeTests
{
    /// <summary>
    /// curl's own offers, sent once after the first read in which the server negotiates:
    /// <c>IAC WILL BINARY</c>, <c>IAC DO BINARY</c>, <c>IAC WILL SGA</c>, <c>IAC DO SGA</c>.
    /// </summary>
    private const string Offers = "FF FB 00 FF FD 00 FF FB 03 FF FD 03";

    /// <summary><c>IAC WILL NAWS</c>.</summary>
    private const string WillNaws = "FF FB 1F";

    private static readonly Uri TelnetUrl = new("telnet://example.test/");

    [TestMethod]
    public async Task ExecuteAsync_WindowSize80x24AndServerDoNaws_SendsWillNawsTheSizeThenOffers()
    {
        Exchange exchange = await RunAsync(["WS=80x24"], Read("FF FD 1F"));

        Assert.AreEqual(WillNaws + " FF FA 1F 00 50 00 18 FF F0 " + Offers, exchange.Sent);
        Assert.AreEqual(CurlExitCode.Ok, exchange.Result.ExitCode);
    }

    [TestMethod]
    [DataRow("WS=255x24", "00 FF FF 00 18", DisplayName = "255 columns")]
    [DataRow("WS=80x255", "00 50 00 FF FF", DisplayName = "255 rows")]
    [DataRow("WS=511x767", "01 FF FF 02 FF FF", DisplayName = "low bytes 0xFF")]
    [DataRow("WS=0x65535", "00 00 FF FF FF FF", DisplayName = "65535 rows")]
    [DataRow("WS=65535x65535", "FF FF FF FF FF FF FF FF", DisplayName = "every byte 0xFF")]
    public async Task ExecuteAsync_WindowSizeWithAn0xFFByte_DoublesIt(string option, string size)
    {
        Exchange exchange = await RunAsync([option], Read("FF FD 1F"));

        Assert.AreEqual(WillNaws + " FF FA 1F " + size + " FF F0 " + Offers, exchange.Sent);
    }

    [TestMethod]
    [DataRow("WS=0x0", DisplayName = "0x0")]
    [DataRow(null, DisplayName = "no WS")]
    public async Task ExecuteAsync_NoOrZeroWindowSizeAndServerDoNaws_AgreesAndSendsZeroSize(string? option)
    {
        Exchange exchange = await RunAsync(option is null ? [] : [option], Read("FF FD 1F"));

        Assert.AreEqual(WillNaws + " FF FA 1F 00 00 00 00 FF F0 " + Offers, exchange.Sent);
    }

    [TestMethod]
    [DataRow("WS=80x24z", DisplayName = "trailing text")]
    [DataRow("WS=080x024", DisplayName = "leading zeros")]
    [DataRow("ws=80x24", DisplayName = "lower-case name")]
    public async Task ExecuteAsync_WindowSizeWrittenLoosely_SendsTheNumbersRead(string option)
    {
        Exchange exchange = await RunAsync([option], Read("FF FD 1F"));

        Assert.AreEqual(WillNaws + " FF FA 1F 00 50 00 18 FF F0 " + Offers, exchange.Sent);
    }

    [TestMethod]
    public async Task ExecuteAsync_TwoWindowSizes_SendsTheLast()
    {
        Exchange exchange = await RunAsync(["WS=80x24", "WS=100x50"], Read("FF FD 1F"));

        Assert.AreEqual(WillNaws + " FF FA 1F 00 64 00 32 FF F0 " + Offers, exchange.Sent);
    }

    [TestMethod]
    public async Task ExecuteAsync_WindowSizeAndServerWillEcho_OffersNawsAfterSga()
    {
        Exchange exchange = await RunAsync(["WS=80x24"], Read("FF FB 01"));

        Assert.AreEqual("FF FD 01 " + Offers + " " + WillNaws, exchange.Sent);
    }

    [TestMethod]
    public async Task ExecuteAsync_NoWindowSizeAndServerWillEcho_DoesNotOfferNaws()
    {
        Exchange exchange = await RunAsync([], Read("FF FB 01"));

        Assert.AreEqual("FF FD 01 " + Offers, exchange.Sent);
    }

    [TestMethod]
    public async Task ExecuteAsync_WindowSizeWithOtherOptions_OffersInOptionNumberOrder()
    {
        Exchange exchange = await RunAsync(
            ["WS=80x24", "TTYPE=vt100", "XDISPLOC=h:0", "NEW_ENV=A,b"],
            Read("FF FB 01"));

        Assert.AreEqual("FF FD 01 " + Offers + " FF FB 18 FF FB 1F FF FB 23 FF FB 27", exchange.Sent);
    }

    [TestMethod]
    public async Task ExecuteAsync_WindowSizeOfferedThenServerDoNaws_SendsOnlyTheSize()
    {
        Exchange exchange = await RunAsync(["WS=80x24"], Read("FF FB 01"), Read("FF FD 1F"));

        Assert.AreEqual("FF FD 01 " + Offers + " " + WillNaws + " FF FA 1F 00 50 00 18 FF F0", exchange.Sent);
    }

    [TestMethod]
    public async Task ExecuteAsync_NoWindowSizeServerWillEchoThenDoNaws_AgreesAndSendsZeroSize()
    {
        Exchange exchange = await RunAsync([], Read("FF FB 01"), Read("FF FD 1F"));

        Assert.AreEqual("FF FD 01 " + Offers + " " + WillNaws + " FF FA 1F 00 00 00 00 FF F0", exchange.Sent);
    }

    [TestMethod]
    public async Task ExecuteAsync_WindowSizeOfferedThenServerDontNaws_SendsNothingMore()
    {
        Exchange exchange = await RunAsync(["WS=80x24"], Read("FF FB 01"), Read("FF FE 1F"));

        Assert.AreEqual("FF FD 01 " + Offers + " " + WillNaws, exchange.Sent);
    }

    [TestMethod]
    public async Task ExecuteAsync_ServerDoNawsTwice_SendsTheSizeOnce()
    {
        Exchange exchange = await RunAsync(["WS=80x24"], Read("FF FD 1F"), Read("FF FD 1F"));

        Assert.AreEqual(WillNaws + " FF FA 1F 00 50 00 18 FF F0 " + Offers, exchange.Sent);
    }

    [TestMethod]
    public async Task ExecuteAsync_ServerDoDontDoNaws_SendsTheSizeEachTimeNawsIsEnabled()
    {
        Exchange exchange = await RunAsync(["WS=80x24"], Read("FF FD 1F"), Read("FF FE 1F"), Read("FF FD 1F"));

        Assert.AreEqual(
            WillNaws + " FF FA 1F 00 50 00 18 FF F0 " + Offers
            + " FF FC 1F " + WillNaws + " FF FA 1F 00 50 00 18 FF F0",
            exchange.Sent);
    }

    [TestMethod]
    public async Task ExecuteAsync_ServerSubnegotiatesNaws_IsIgnored()
    {
        Exchange exchange = await RunAsync(["WS=80x24"], Read("FF FD 1F"), Read("FF FA 1F 01 FF F0"));

        Assert.AreEqual(WillNaws + " FF FA 1F 00 50 00 18 FF F0 " + Offers, exchange.Sent);
    }

    [TestMethod]
    public async Task ExecuteAsync_WindowSizeWithBinaryZero_SendsTheSizeThenOffersOnlySga()
    {
        Exchange exchange = await RunAsync(["WS=80x24", "BINARY=0"], Read("FF FD 1F"));

        Assert.AreEqual(WillNaws + " FF FA 1F 00 50 00 18 FF F0 FF FB 03 FF FD 03", exchange.Sent);
    }

    private static ScriptedRead Read(string hex) => new(Convert.FromHexString(hex.Replace(" ", string.Empty, StringComparison.Ordinal)));

    private static string ToHex(byte[] bytes) =>
        string.Join(' ', Convert.ToHexString(bytes).Chunk(2).Select(pair => new string(pair)));

    private static async Task<Exchange> RunAsync(string[] telnetOptions, params ScriptedRead[] reads)
    {
        var connection = new ScriptedConnection(reads);
        var context = new TransferContext
        {
            Url = TelnetUrl,
            Output = new MemoryStream(),
            Upload = new MemoryStream(),
            TelnetOptions = telnetOptions,
        };

        TransferResult result = await new TelnetProtocolHandler(new RecordingConnector(ConnectResult.Connected(connection)))
            .ExecuteAsync(context);

        Assert.IsTrue(connection.IsDisposed);
        return new Exchange(result, ToHex(connection.Sent));
    }

    private sealed record Exchange(TransferResult Result, string Sent);
}
