using System.Net;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Telnet.Fakes;
using Curl.Testing;

namespace Curl.Protocol.Telnet;

/// <summary>
/// Pins what <c>-u</c>/<c>--user</c> does to a <c>telnet://</c> session against curl
/// 8.21.0, measured on 2026-09-26 against a loopback listener (captures in BL-084's
/// Notes): the user name is sent as the NEW-ENVIRON variable <c>USER</c>, ahead of any
/// <c>-t NEW_ENV</c> variable.
/// </summary>
[TestClass]
public sealed class TelnetProtocolHandlerUserNameTests
{
    /// <summary>
    /// curl's own offers, sent once after the first read in which the server negotiates:
    /// <c>IAC WILL BINARY</c>, <c>IAC DO BINARY</c>, <c>IAC WILL SGA</c>, <c>IAC DO SGA</c>.
    /// </summary>
    private const string Offers = "FF FB 00 FF FD 00 FF FB 03 FF FD 03";

    /// <summary><c>IAC SB NEW-ENVIRON IS VAR "USER" VALUE</c>.</summary>
    private const string UserVariableStart = "FF FA 27 00 00 55 53 45 52 01";

    private static readonly CurlUrl TelnetUrl = CurlUrl.Parse("telnet://example.test/");

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public async Task ExecuteAsync_UserNameAndNewEnvironmentAskedFor_SendsWillNewEnvironOffersAndUser()
    {
        Exchange exchange = await RunAsync("bob", [], Read("FF FD 27"), Read("FF FA 27 01 FF F0"));

        Diagnostics.Diff("sent", "FF FB 27 " + Offers + " " + UserVariableStart + " 62 6F 62 FF F0", exchange.Sent);
        Diagnostics.AssertExitCode(CurlExitCode.Ok, exchange.Result.ExitCode);
        Assert.AreEqual("FF FB 27 " + Offers + " " + UserVariableStart + " 62 6F 62 FF F0", exchange.Sent);
        Assert.AreEqual(CurlExitCode.Ok, exchange.Result.ExitCode);
    }

    [TestMethod]
    public async Task ExecuteAsync_UserNameAndNewEnvironmentOption_SendsUserFirst()
    {
        Exchange exchange = await RunAsync(
            "bob",
            ["NEW_ENV=TERM,vt100"],
            Read("FF FD 27"),
            Read("FF FA 27 01 FF F0"));

        Diagnostics.Diff("sent", "FF FB 27 " + Offers + " " + UserVariableStart + " 62 6F 62 00 54 45 52 4D 01 76 74 31 30 30 FF F0", exchange.Sent);
        Diagnostics.AssertExitCode(CurlExitCode.Ok, exchange.Result.ExitCode);
        Assert.AreEqual(
            "FF FB 27 " + Offers + " " + UserVariableStart + " 62 6F 62 00 54 45 52 4D 01 76 74 31 30 30 FF F0",
            exchange.Sent);
        Assert.AreEqual(CurlExitCode.Ok, exchange.Result.ExitCode);
    }

    [TestMethod]
    public async Task ExecuteAsync_UserNameAndServerWillEcho_OffersNewEnvironAfterSga()
    {
        Exchange exchange = await RunAsync("bob", [], Read("FF FB 01"));

        Diagnostics.Diff("sent", "FF FD 01 " + Offers + " FF FB 27", exchange.Sent);
        Assert.AreEqual("FF FD 01 " + Offers + " FF FB 27", exchange.Sent);
    }

    [TestMethod]
    [DataRow("", "", DisplayName = "empty user name")]
    [DataRow("a,b", " 61 2C 62", DisplayName = "user name with a comma")]
    [DataRow("b%C3%A9", " 62 25 43 33 25 41 39", DisplayName = "percent sign not decoded")]
    public async Task ExecuteAsync_UserName_IsSentAsGiven(string userName, string expectedValue)
    {
        Exchange exchange = await RunAsync(userName, [], Read("FF FA 27 01 FF F0"));

        Diagnostics.Diff("sent", UserVariableStart + expectedValue + " FF F0", exchange.Sent);
        Assert.AreEqual(UserVariableStart + expectedValue + " FF F0", exchange.Sent);
    }

    [TestMethod]
    [DataRow(250, 250, DisplayName = "250 characters, all sent")]
    [DataRow(251, 250, DisplayName = "251 characters, cut to 250")]
    [DataRow(300, 250, DisplayName = "300 characters, cut to 250")]
    public async Task ExecuteAsync_LongUserName_IsCutTo250Characters(int length, int expectedLength)
    {
        Exchange exchange = await RunAsync(new string('A', length), [], Read("FF FA 27 01 FF F0"));

        Diagnostics.Assert("sent length", 10 + expectedLength + 2, exchange.SentBytes.Length);
        Diagnostics.Diff("sent start", UserVariableStart + " 41", exchange.Sent[..Math.Min(exchange.Sent.Length, UserVariableStart.Length + 3)]);
        Diagnostics.Diff("sent end", "41 FF F0", exchange.Sent[Math.Max(0, exchange.Sent.Length - 8)..]);
        Assert.HasCount(10 + expectedLength + 2, exchange.SentBytes);
        Assert.StartsWith(UserVariableStart + " 41", exchange.Sent);
        Assert.EndsWith("41 FF F0", exchange.Sent);
    }

    [TestMethod]
    [DataRow(new string[0], DisplayName = "no -t option")]
    [DataRow(new[] { "BOGUS=1" }, DisplayName = "before an unknown option")]
    [DataRow(new[] { "TTYPE" }, DisplayName = "before an option without =")]
    public async Task ExecuteAsync_NonAsciiUserName_ExitsWith43AfterConnectingAndSendsNothing(string[] telnetOptions)
    {
        var connection = new ScriptedConnection(Read("FF FD 27"), Read("FF FA 27 01 FF F0"));
        var connector = new RecordingConnector(ConnectResult.Connected(connection));
        var output = new MemoryStream();
        Diagnostics.Arrange("user name (-u)", "b\\xe9");
        Diagnostics.Arrange("telnet options (-t)", telnetOptions.Length == 0 ? "(none)" : string.Join(" | ", telnetOptions));
        Diagnostics.ArrangeReads([Read("FF FD 27"), Read("FF FA 27 01 FF F0")]);

        TransferResult result = await new TelnetProtocolHandler(connector).ExecuteAsync(
            new TransferContext
            {
                Url = TelnetUrl,
                Output = output,
                Credentials = new NetworkCredential("bé", "x"),
                TelnetOptions = telnetOptions,
            });

        Diagnostics.ActResult(result);
        Diagnostics.ActSent(connection.Sent);
        Diagnostics.Act("connects", connector.Targets.Count);
        Diagnostics.AssertExitCode(CurlExitCode.BadFunctionArgument, result.ExitCode);
        Diagnostics.Diff("error", "A libcurl function was given a bad argument", result.ErrorMessage ?? string.Empty);
        Diagnostics.Assert("connects", 1, connector.Targets.Count);
        Assert.AreEqual(CurlExitCode.BadFunctionArgument, result.ExitCode);
        Assert.AreEqual("A libcurl function was given a bad argument", result.ErrorMessage);
        Assert.HasCount(1, connector.Targets);
        Assert.IsEmpty(connection.Sent);
        Assert.AreEqual(0L, output.Length);
        Assert.IsTrue(connection.IsDisposed);
    }

    [TestMethod]
    public async Task ExecuteAsync_AsciiUserNameAndOptionWithoutEquals_ExitsWith49()
    {
        Exchange exchange = await RunAsync("bob", ["TTYPE"], Read("FF FD 27"));

        Diagnostics.AssertExitCode(CurlExitCode.SetoptOptionSyntax, exchange.Result.ExitCode);
        Diagnostics.Diff("sent", string.Empty, exchange.Sent);
        Assert.AreEqual(CurlExitCode.SetoptOptionSyntax, exchange.Result.ExitCode);
        Assert.AreEqual(string.Empty, exchange.Sent);
    }

    private static ScriptedRead Read(string hex) => new(Convert.FromHexString(hex.Replace(" ", string.Empty, StringComparison.Ordinal)));

    private static string ToHex(byte[] bytes) =>
        string.Join(' ', Convert.ToHexString(bytes).Chunk(2).Select(pair => new string(pair)));

    private async Task<Exchange> RunAsync(string userName, string[] telnetOptions, params ScriptedRead[] reads)
    {
        var connection = new ScriptedConnection(reads);
        var context = new TransferContext
        {
            Url = TelnetUrl,
            Output = new MemoryStream(),
            Upload = new MemoryStream(),
            Credentials = new NetworkCredential(userName, "x"),
            TelnetOptions = telnetOptions,
        };
        Diagnostics.ArrangeContext(context);
        Diagnostics.Arrange("user name (-u)", $"{userName.Length} characters: {(userName.Length > 40 ? userName[..40] + "..." : userName)}");
        Diagnostics.ArrangeReads(reads);

        TransferResult result = await new TelnetProtocolHandler(new RecordingConnector(ConnectResult.Connected(connection)))
            .ExecuteAsync(context);

        Diagnostics.ActResult(result);
        Diagnostics.ActSent(connection.Sent);
        Assert.IsTrue(connection.IsDisposed);
        byte[] sent = connection.Sent;
        return new Exchange(result, sent, ToHex(sent));
    }

    private sealed record Exchange(TransferResult Result, byte[] SentBytes, string Sent);
}
