using Curl.Protocol.Abstractions;
using Curl.Protocol.Telnet.Fakes;

namespace Curl.Protocol.Telnet;

/// <summary>
/// Pins what <c>-t</c>/<c>--telnet-option</c> does to a <c>telnet://</c> session against
/// curl 8.21.0, measured on 2026-09-26 against a loopback listener (captures in BL-044's
/// Notes): the bytes sent, the bytes written and the exit code.
/// </summary>
[TestClass]
public sealed class TelnetProtocolHandlerTelnetOptionTests
{
    /// <summary>
    /// curl's own offers, sent once after the first read in which the server negotiates:
    /// <c>IAC WILL BINARY</c>, <c>IAC DO BINARY</c>, <c>IAC WILL SGA</c>, <c>IAC DO SGA</c>.
    /// </summary>
    private const string Offers = "FF FB 00 FF FD 00 FF FB 03 FF FD 03";

    private const string GenericUnknownOptionMessage = "An unknown option was passed in to libcurl";

    private static readonly Uri TelnetUrl = new("telnet://example.test/");

    [TestMethod]
    public async Task ExecuteAsync_TerminalTypeAskedFor_SendsWillTtypeOffersAndTheLowerCaseOptionsValue()
    {
        Exchange exchange = await RunAsync(["ttype=vt100"], Read("FF FD 18"), Read("FF FA 18 01 FF F0"));

        Assert.AreEqual("FF FB 18 " + Offers + " FF FA 18 00 76 74 31 30 30 FF F0", exchange.Sent);
        Assert.AreEqual(string.Empty, exchange.Output);
        Assert.AreEqual(CurlExitCode.Ok, exchange.Result.ExitCode);
    }

    [TestMethod]
    public async Task ExecuteAsync_XDisplayLocationAskedFor_SendsWillXdisplocOffersAndTheValue()
    {
        Exchange exchange = await RunAsync(["XDISPLOC=host:0"], Read("FF FD 23"), Read("FF FA 23 01 FF F0"));

        Assert.AreEqual("FF FB 23 " + Offers + " FF FA 23 00 68 6F 73 74 3A 30 FF F0", exchange.Sent);
        Assert.AreEqual(CurlExitCode.Ok, exchange.Result.ExitCode);
    }

    [TestMethod]
    public async Task ExecuteAsync_NewEnvironmentAskedFor_SendsWillNewEnvironOffersAndTheVariable()
    {
        Exchange exchange = await RunAsync(["NEW_ENV=USER,bob"], Read("FF FD 27"), Read("FF FA 27 01 FF F0"));

        Assert.AreEqual("FF FB 27 " + Offers + " FF FA 27 00 00 55 53 45 52 01 62 6F 62 FF F0", exchange.Sent);
        Assert.AreEqual(CurlExitCode.Ok, exchange.Result.ExitCode);
    }

    [TestMethod]
    public async Task ExecuteAsync_SeveralNewEnvironmentVariables_SendsEachInOrderSplitAtTheFirstComma()
    {
        Exchange exchange = await RunAsync(
            ["NEW_ENV=USER,bob", "new_env=TERM", "NEW_ENV=A,b,c"],
            Read("FF FD 27"),
            Read("FF FA 27 01 FF F0"));

        Assert.AreEqual(
            "FF FB 27 " + Offers + " FF FA 27 00 00 55 53 45 52 01 62 6F 62 00 54 45 52 4D 00 41 01 62 2C 63 FF F0",
            exchange.Sent);
    }

    [TestMethod]
    public async Task ExecuteAsync_NewEnvironmentVariablesWithEmptyParts_SendsThemEmpty()
    {
        Exchange exchange = await RunAsync(["NEW_ENV=,x", "NEW_ENV=", "NEW_ENV=a,"], Read("FF FA 27 01 FF F0"));

        Assert.AreEqual("FF FA 27 00 00 01 78 00 00 61 01 FF F0", exchange.Sent);
    }

    [TestMethod]
    public async Task ExecuteAsync_AllThreeAskedFor_AnswersEachDoThenEachSubnegotiation()
    {
        Exchange exchange = await RunAsync(
            ["TTYPE=vt100", "XDISPLOC=host:0", "NEW_ENV=USER,bob"],
            Read("FF FD 18 FF FD 23 FF FD 27"),
            Read("FF FA 18 01 FF F0 FF FA 23 01 FF F0 FF FA 27 01 FF F0"));

        Assert.AreEqual(
            "FF FB 18 FF FB 23 FF FB 27 " + Offers
            + " FF FA 18 00 76 74 31 30 30 FF F0"
            + " FF FA 23 00 68 6F 73 74 3A 30 FF F0"
            + " FF FA 27 00 00 55 53 45 52 01 62 6F 62 FF F0",
            exchange.Sent);
        Assert.AreEqual(CurlExitCode.Ok, exchange.Result.ExitCode);
    }

    [TestMethod]
    public async Task ExecuteAsync_AllThreeGivenAndServerWillEcho_OffersThemAfterSga()
    {
        Exchange exchange = await RunAsync(
            ["TTYPE=vt100", "XDISPLOC=host:0", "NEW_ENV=USER,bob"],
            Read("FF FB 01"));

        Assert.AreEqual("FF FD 01 " + Offers + " FF FB 18 FF FB 23 FF FB 27", exchange.Sent);
    }

    [TestMethod]
    [DataRow("FF FA 18 00 FF F0", DisplayName = "TTYPE with IS instead of SEND")]
    [DataRow("FF FA 18 FF F0", DisplayName = "TTYPE with no qualifier")]
    public async Task ExecuteAsync_TerminalTypeSubnegotiationWithoutSend_IsAnsweredAnyway(string received)
    {
        Exchange exchange = await RunAsync(["TTYPE=x"], Read(received));

        Assert.AreEqual("FF FA 18 00 78 FF F0", exchange.Sent);
    }

    [TestMethod]
    public async Task ExecuteAsync_NewEnvironmentSubnegotiationWithInfo_IsAnsweredAnyway()
    {
        Exchange exchange = await RunAsync(["NEW_ENV=x"], Read("FF FA 27 02 FF F0"));

        Assert.AreEqual("FF FA 27 00 00 78 FF F0", exchange.Sent);
    }

    [TestMethod]
    public async Task ExecuteAsync_UnhandledSubnegotiationWithOptions_IsIgnored()
    {
        Exchange exchange = await RunAsync(["TTYPE=x"], Read("FF FA 05 FF F0"));

        Assert.AreEqual(string.Empty, exchange.Sent);
        Assert.AreEqual(CurlExitCode.Ok, exchange.Result.ExitCode);
    }

    [TestMethod]
    public async Task ExecuteAsync_EmptyTerminalType_SendsAnEmptyValue()
    {
        Exchange exchange = await RunAsync(["TTYPE="], Read("FF FA 18 01 FF F0"));

        Assert.AreEqual("FF FA 18 00 FF F0", exchange.Sent);
    }

    [TestMethod]
    [DataRow("ttype=é", DisplayName = "TTYPE value with a non-ASCII letter")]
    [DataRow("TTYPE=aÿ", DisplayName = "TTYPE value with FF")]
    public async Task ExecuteAsync_NonAsciiTerminalType_IsSkippedSoTheSubnegotiationExitsWith43(string option)
    {
        Exchange exchange = await RunAsync([option], Read("FF FA 18 01 FF F0"));

        Assert.AreEqual(CurlExitCode.BadFunctionArgument, exchange.Result.ExitCode);
        Assert.AreEqual(string.Empty, exchange.Sent);
    }

    [TestMethod]
    public async Task ExecuteAsync_TerminalTypeOf1000Characters_IsSent()
    {
        Exchange exchange = await RunAsync(["TTYPE=" + new string('a', 1000)], Read("FF FA 18 01 FF F0"));

        Assert.AreEqual(CurlExitCode.Ok, exchange.Result.ExitCode);
        Assert.HasCount(1006, exchange.SentBytes);
    }

    [TestMethod]
    [DataRow("TTYPE=", "FF FA 18 01 FF F0", "Too long telnet TTYPE", DisplayName = "TTYPE")]
    [DataRow("XDISPLOC=", "FF FA 23 01 FF F0", "Too long telnet XDISPLOC", DisplayName = "XDISPLOC")]
    public async Task ExecuteAsync_ValueOf1001CharactersAskedFor_ExitsWith55(
        string optionName,
        string received,
        string expectedMessage)
    {
        Exchange exchange = await RunAsync([optionName + new string('a', 1001)], Read("61 " + received), Read("62"));

        Assert.AreEqual(CurlExitCode.SendError, exchange.Result.ExitCode);
        Assert.AreEqual(expectedMessage, exchange.Result.ErrorMessage);
        Assert.AreEqual("61", exchange.Output);
        Assert.AreEqual(1L, exchange.Result.BytesTransferred);
        Assert.AreEqual(string.Empty, exchange.Sent);
    }

    [TestMethod]
    [DataRow(2036, 2043, DisplayName = "2036 characters fit")]
    [DataRow(2037, 6, DisplayName = "2037 characters are left out")]
    public async Task ExecuteAsync_OneLongNewEnvironmentVariable_IsSentOnlyIfItFits(int length, int expectedSentLength)
    {
        Exchange exchange = await RunAsync(["NEW_ENV=" + new string('a', length)], Read("FF FA 27 01 FF F0"));

        Assert.HasCount(expectedSentLength, exchange.SentBytes);
        Assert.AreEqual("FF FA 27 00", ToHex(exchange.SentBytes[..4]));
        Assert.AreEqual("FF F0", ToHex(exchange.SentBytes[^2..]));
    }

    [TestMethod]
    [DataRow(1035, 2043, "62 FF F0", DisplayName = "second fits, third does not")]
    [DataRow(1036, 1009, "00 63 FF F0", DisplayName = "second is left out, third still fits")]
    public async Task ExecuteAsync_NewEnvironmentVariablesPastTheLimit_AreLeftOutAndTheNextTried(
        int secondLength,
        int expectedSentLength,
        string expectedEnd)
    {
        Exchange exchange = await RunAsync(
            ["NEW_ENV=" + new string('a', 1000), "NEW_ENV=" + new string('b', secondLength), "NEW_ENV=c"],
            Read("FF FA 27 01 FF F0"));

        Assert.HasCount(expectedSentLength, exchange.SentBytes);
        Assert.EndsWith(expectedEnd, exchange.Sent);
    }

    [TestMethod]
    public async Task ExecuteAsync_UnknownOption_ExitsWith48AfterConnectingAndSendsNothing()
    {
        var connection = new ScriptedConnection(Read("68 69"));
        var connector = new RecordingConnector(ConnectResult.Connected(connection));
        var output = new MemoryStream();

        TransferResult result = await new TelnetProtocolHandler(connector).ExecuteAsync(
            new TransferContext { Url = TelnetUrl, Output = output, TelnetOptions = ["BOGUS=1"] });

        Assert.AreEqual(CurlExitCode.UnknownOption, result.ExitCode);
        Assert.AreEqual(GenericUnknownOptionMessage, result.ErrorMessage);
        Assert.AreEqual(0L, result.BytesTransferred);
        Assert.HasCount(1, connector.Targets);
        Assert.IsEmpty(connection.Sent);
        Assert.AreEqual(0L, output.Length);
        Assert.IsTrue(connection.IsDisposed);
    }

    [TestMethod]
    public async Task ExecuteAsync_OptionWithoutEquals_ExitsWith49AfterConnectingAndSendsNothing()
    {
        var connection = new ScriptedConnection(Read("68 69"));
        var connector = new RecordingConnector(ConnectResult.Connected(connection));
        var output = new MemoryStream();

        TransferResult result = await new TelnetProtocolHandler(connector).ExecuteAsync(
            new TransferContext { Url = TelnetUrl, Output = output, TelnetOptions = ["TTYPE"] });

        Assert.AreEqual(CurlExitCode.SetoptOptionSyntax, result.ExitCode);
        Assert.AreEqual("Syntax error in telnet option: TTYPE", result.ErrorMessage);
        Assert.HasCount(1, connector.Targets);
        Assert.IsEmpty(connection.Sent);
        Assert.AreEqual(0L, output.Length);
        Assert.IsTrue(connection.IsDisposed);
    }

    [TestMethod]
    [DataRow("BOGU=1", "Unknown telnet option BOGU=1", DisplayName = "4-byte name gets the telnet message")]
    [DataRow("BOG=1", "Unknown telnet option BOG=1", DisplayName = "3-byte name")]
    [DataRow("BOGUSXYZZ=1", "Unknown telnet option BOGUSXYZZ=1", DisplayName = "9-byte name")]
    [DataRow("=1", "Unknown telnet option =1", DisplayName = "empty name")]
    [DataRow("Ãœ=1", "Unknown telnet option Ãœ=1", DisplayName = "4 UTF-8 bytes in 2 characters")]
    [DataRow("TTYPX=1", GenericUnknownOptionMessage, DisplayName = "5-byte name gets the generic message")]
    [DataRow("XDISPLOX=1", GenericUnknownOptionMessage, DisplayName = "8-byte name")]
    [DataRow("NEW_ENX=1", GenericUnknownOptionMessage, DisplayName = "7-byte name")]
    [DataRow("WX=80x24", GenericUnknownOptionMessage, DisplayName = "2-byte name")]
    [DataRow("BINARZ=1", GenericUnknownOptionMessage, DisplayName = "6-byte name")]
    [DataRow("BÃœ=1", GenericUnknownOptionMessage, DisplayName = "5 UTF-8 bytes in 3 characters")]
    public async Task ExecuteAsync_UnknownOptionName_ExitsWith48WithCurlsMessageForItsLength(
        string option,
        string expectedMessage)
    {
        Exchange exchange = await RunAsync([option], Read("68 69"));

        Assert.AreEqual(CurlExitCode.UnknownOption, exchange.Result.ExitCode);
        Assert.AreEqual(expectedMessage, exchange.Result.ErrorMessage);
        Assert.AreEqual(string.Empty, exchange.Output);
    }

    [TestMethod]
    [DataRow("WS=80", DisplayName = "no rows")]
    [DataRow("WS=80x", DisplayName = "empty rows")]
    [DataRow("WS=x24", DisplayName = "no columns")]
    [DataRow("WS=80X24", DisplayName = "upper-case X")]
    [DataRow("WS=80y24", DisplayName = "wrong separator")]
    [DataRow("WS=70000x24", DisplayName = "columns over 65535")]
    [DataRow("WS=65536x1", DisplayName = "columns 65536")]
    [DataRow("WS=99999999999999999999999x1", DisplayName = "columns past any integer")]
    [DataRow("WS=80x70000", DisplayName = "rows over 65535")]
    [DataRow("WS=80x-1", DisplayName = "negative rows")]
    [DataRow("WS= +80x 24", DisplayName = "space and sign")]
    [DataRow("BINARY", DisplayName = "BINARY without equals")]
    public async Task ExecuteAsync_MalformedOption_ExitsWith49NamingIt(string option)
    {
        Exchange exchange = await RunAsync([option], Read("68 69"));

        Assert.AreEqual(CurlExitCode.SetoptOptionSyntax, exchange.Result.ExitCode);
        Assert.AreEqual("Syntax error in telnet option: " + option, exchange.Result.ErrorMessage);
        Assert.AreEqual(string.Empty, exchange.Output);
    }

    [TestMethod]
    [DataRow(new[] { "TTYPE=vt100", "BOGUS=1", "TTYPE" }, CurlExitCode.UnknownOption, DisplayName = "unknown before malformed")]
    [DataRow(new[] { "TTYPE", "BOGUS=1" }, CurlExitCode.SetoptOptionSyntax, DisplayName = "malformed before unknown")]
    [DataRow(new[] { "TTYPE=1", "WS=1" }, CurlExitCode.SetoptOptionSyntax, DisplayName = "good then bad")]
    public async Task ExecuteAsync_SeveralBadOptions_TheFirstDecidesTheExitCode(string[] options, CurlExitCode expected)
    {
        Exchange exchange = await RunAsync(options, Read("68 69"));

        Assert.AreEqual(expected, exchange.Result.ExitCode);
    }

    [TestMethod]
    [DataRow("ws=80x24", DisplayName = "lower-case WS")]
    [DataRow("WS=80x24z", DisplayName = "WS with trailing text")]
    [DataRow("WS=080x024", DisplayName = "WS with leading zeros")]
    [DataRow("WS=65535x65535", DisplayName = "WS at its limits")]
    [DataRow("WS=80x0", DisplayName = "WS with zero rows")]
    [DataRow("WS=0x24", DisplayName = "WS with zero columns")]
    [DataRow("BINARY=0", DisplayName = "BINARY=0")]
    [DataRow("BINARY=x", DisplayName = "BINARY with any value")]
    [DataRow("BINARY=", DisplayName = "BINARY empty")]
    [DataRow("binary=1", DisplayName = "lower-case BINARY")]
    [DataRow("BOGUS=é", DisplayName = "unknown name with a non-ASCII value is skipped")]
    public async Task ExecuteAsync_AcceptedOptionNotNegotiated_RunsTheSessionUnchanged(string option)
    {
        Exchange exchange = await RunAsync([option], Read("68 69"));

        Assert.AreEqual(CurlExitCode.Ok, exchange.Result.ExitCode);
        Assert.AreEqual("68 69", exchange.Output);
        Assert.AreEqual(string.Empty, exchange.Sent);
    }

    [TestMethod]
    public async Task ExecuteAsync_BinaryZeroAndServerSendsDoThenWillBinary_RefusesBothAndOffersOnlySga()
    {
        Exchange exchange = await RunAsync(["BINARY=0"], Read("FF FD 00"), Read("FF FB 00"));

        Assert.AreEqual("FF FC 00 FF FB 03 FF FD 03 FF FE 00", exchange.Sent);
        Assert.AreEqual(CurlExitCode.Ok, exchange.Result.ExitCode);
    }

    [TestMethod]
    public async Task ExecuteAsync_BinaryZeroAndServerSendsDoAndWillBinaryInOneRead_RefusesBothThenOffersSga()
    {
        Exchange exchange = await RunAsync(["BINARY=0"], Read("FF FD 00 FF FB 00"));

        Assert.AreEqual("FF FC 00 FF FE 00 FF FB 03 FF FD 03", exchange.Sent);
    }

    [TestMethod]
    public async Task ExecuteAsync_BinaryZeroAndServerWillEcho_OffersOnlySga()
    {
        Exchange exchange = await RunAsync(["BINARY=0"], Read("FF FB 01"));

        Assert.AreEqual("FF FD 01 FF FB 03 FF FD 03", exchange.Sent);
    }

    [TestMethod]
    public async Task ExecuteAsync_BinaryZeroWithTerminalType_OffersTtypeAfterSga()
    {
        Exchange exchange = await RunAsync(["BINARY=0", "TTYPE=vt"], Read("FF FD 00"), Read("FF FB 00"));

        Assert.AreEqual("FF FC 00 FF FB 03 FF FD 03 FF FB 18 FF FE 00", exchange.Sent);
    }

    [TestMethod]
    [DataRow(new[] { "BINARY=0" }, false, DisplayName = "0 refuses")]
    [DataRow(new[] { "binary=0" }, false, DisplayName = "lower-case name refuses")]
    [DataRow(new[] { "BINARY=00" }, false, DisplayName = "00 refuses")]
    [DataRow(new[] { "BINARY=000000000000000000000" }, false, DisplayName = "many zeros refuse")]
    [DataRow(new[] { "BINARY=0x" }, false, DisplayName = "0 then a letter refuses")]
    [DataRow(new[] { "BINARY=0 " }, false, DisplayName = "0 then a space refuses")]
    [DataRow(new[] { "BINARY=0,1" }, false, DisplayName = "0 then a comma refuses")]
    [DataRow(new[] { "BINARY=0", "BINARY=1" }, false, DisplayName = "a later 1 does not undo a 0")]
    [DataRow(new[] { "BINARY=1", "BINARY=0" }, false, DisplayName = "a later 0 refuses")]
    [DataRow(new[] { "BINARY=1" }, true, DisplayName = "1 keeps it")]
    [DataRow(new[] { "BINARY=01" }, true, DisplayName = "01 keeps it")]
    [DataRow(new[] { "BINARY=00001" }, true, DisplayName = "00001 keeps it")]
    [DataRow(new[] { "BINARY=1x" }, true, DisplayName = "1x keeps it")]
    [DataRow(new[] { "BINARY= 1" }, true, DisplayName = "space then 1 keeps it")]
    [DataRow(new[] { "BINARY= 0" }, true, DisplayName = "space then 0 keeps it")]
    [DataRow(new[] { "BINARY=	0" }, true, DisplayName = "tab then 0 keeps it")]
    [DataRow(new[] { "BINARY=+0" }, true, DisplayName = "+0 keeps it")]
    [DataRow(new[] { "BINARY=-0" }, true, DisplayName = "-0 keeps it")]
    [DataRow(new[] { "BINARY=+1" }, true, DisplayName = "+1 keeps it")]
    [DataRow(new[] { "BINARY=-1" }, true, DisplayName = "-1 keeps it")]
    [DataRow(new[] { "BINARY=2" }, true, DisplayName = "2 keeps it")]
    [DataRow(new[] { "BINARY=4294967297" }, true, DisplayName = "2^32 + 1 keeps it")]
    [DataRow(new[] { "BINARY=x" }, true, DisplayName = "a letter keeps it")]
    [DataRow(new[] { "BINARY=" }, true, DisplayName = "empty keeps it")]
    public async Task ExecuteAsync_BinaryValue_KeepsOrRefusesBinaryAsCurlDoes(string[] options, bool binaryKept)
    {
        Exchange exchange = await RunAsync(options, Read("FF FD 00"), Read("FF FB 00"));

        string expected = binaryKept ? Offers : "FF FC 00 FF FB 03 FF FD 03 FF FE 00";
        Assert.AreEqual(expected, exchange.Sent);
        Assert.AreEqual(CurlExitCode.Ok, exchange.Result.ExitCode);
    }

    private static ScriptedRead Read(string hex) => new(Convert.FromHexString(hex.Replace(" ", string.Empty, StringComparison.Ordinal)));

    private static string ToHex(byte[] bytes) =>
        string.Join(' ', Convert.ToHexString(bytes).Chunk(2).Select(pair => new string(pair)));

    private static async Task<Exchange> RunAsync(string[] telnetOptions, params ScriptedRead[] reads)
    {
        var connection = new ScriptedConnection(reads);
        var output = new MemoryStream();
        var context = new TransferContext
        {
            Url = TelnetUrl,
            Output = output,
            Upload = new MemoryStream(),
            TelnetOptions = telnetOptions,
        };

        TransferResult result = await new TelnetProtocolHandler(new RecordingConnector(ConnectResult.Connected(connection)))
            .ExecuteAsync(context);

        Assert.IsTrue(connection.IsDisposed);
        byte[] sent = connection.Sent;
        return new Exchange(result, sent, ToHex(sent), ToHex(output.ToArray()));
    }

    private sealed record Exchange(TransferResult Result, byte[] SentBytes, string Sent, string Output);
}
