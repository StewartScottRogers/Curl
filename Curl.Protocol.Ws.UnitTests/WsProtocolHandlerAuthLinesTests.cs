using System.Net;
using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Ws.Fakes;
using Curl.Testing;
using HttpRequestOptions = Curl.Protocol.Abstractions.HttpRequestOptions;

namespace Curl.Protocol.Ws;

/// <summary>
/// Pins the <c>-v</c> auth lines of a WebSocket upgrade against curl 8.21.0 Schannel, measured on
/// 2026-09-30 with <c>Record-CurlExchange.ps1</c> for <c>ws://127.0.0.1:18953/c -v -u u:p</c>
/// (BL-953 Notes): <c>Server auth using &lt;scheme&gt; with user 'u'</c> after
/// <c>using HTTP/1.x</c> for <c>--digest</c>, <c>--ntlm</c> and <c>--basic</c>, none for
/// <c>--anyauth</c>, and for a refused Basic value <c>Basic authentication problem, ignoring.</c>
/// just before the 401's <c>WWW-Authenticate: Basic</c> header, ahead of
/// <c>Refused WebSocket upgrade: 401</c>. The authenticator is a fake giving the value each
/// scheme makes.
/// </summary>
[TestClass]
public sealed class WsProtocolHandlerAuthLinesTests
{
    private const string BasicDenied = "HTTP/1.1 401 Unauthorized\r\nWWW-Authenticate: Basic realm=\"x\"\r\nContent-Length: 0\r\n\r\n";

    private const string Head101 = "HTTP/1.1 101 Switching Protocols\r\nUpgrade: websocket\r\nConnection: Upgrade\r\n\r\n";

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [DataRow(HttpAuthSchemes.Digest, null, "* Server auth using Digest with user 'u'")]
    [DataRow(HttpAuthSchemes.Ntlm, "NTLM TlRMTVNTUAABAAAA", "* Server auth using NTLM with user 'u'")]
    [DataRow(HttpAuthSchemes.Basic, "Basic dTpw", "* Server auth using Basic with user 'u'")]
    public async Task ExecuteAsync_SchemePickedVerbose_WritesServerAuthUsingBeforeTheRequest(HttpAuthSchemes schemes, string? authorization, string expected)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("schemes", schemes);
        diagnostics.Arrange("authorization value", authorization ?? "none");
        diagnostics.Bytes("scripted 401 reply", Encoding.Latin1.GetBytes(BasicDenied));

        RecordingTransferEvents events = await RunAsync(BasicDenied, new RecordingAuthenticator(authorization), schemes);

        string[] expectedLines = ["* using HTTP/1.x", expected];
        diagnostics.Act("events", Show(events.Transcript));
        diagnostics.Assert("first two events", Show(expectedLines), Show(events.Transcript.Take(2)));
        diagnostics.Assert("third event starts with", "> GET /c HTTP/1.1", events.Transcript[2]);
        CollectionAssert.AreEqual(expectedLines, events.Transcript.Take(2).ToArray());
        Assert.StartsWith("> GET /c HTTP/1.1", events.Transcript[2]);
    }

    [TestMethod]
    public async Task ExecuteAsync_BasicRefusedVerbose_WritesTheProblemBeforeTheChallengeHeader()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("schemes", HttpAuthSchemes.Basic);
        diagnostics.Bytes("scripted 401 reply", Encoding.Latin1.GetBytes(BasicDenied));

        RecordingTransferEvents events = await RunAsync(BasicDenied, new RecordingAuthenticator("Basic dTpw"), HttpAuthSchemes.Basic);

        string[] expected =
        [
            "* Request completely sent off",
            "< HTTP/1.1 401 Unauthorized\r\n",
            "* Basic authentication problem, ignoring.",
            "< WWW-Authenticate: Basic realm=\"x\"\r\n",
            "< Content-Length: 0\r\n",
            "* Refused WebSocket upgrade: 401",
            "< \r\n",
            "* closing connection #0",
        ];
        diagnostics.Act("events", Show(events.Transcript));
        diagnostics.Assert("events after the request", Show(expected), Show(events.Transcript.Skip(3)));
        CollectionAssert.AreEqual(expected, events.Transcript.Skip(3).ToArray());
    }

    [TestMethod]
    public async Task ExecuteAsync_BasicReplacedByHeaderAndRefusedVerbose_WritesOnlyTheProblem()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("schemes", HttpAuthSchemes.Basic);
        diagnostics.Arrange("header", "Authorization: X y");
        diagnostics.Bytes("scripted 401 reply", Encoding.Latin1.GetBytes(BasicDenied));

        RecordingTransferEvents events = await RunAsync(BasicDenied, new RecordingAuthenticator("Basic dTpw"), HttpAuthSchemes.Basic, "Authorization: X y");

        bool serverAuth = events.Transcript.Any(line => line.StartsWith("* Server auth using", StringComparison.Ordinal));
        diagnostics.Act("events", Show(events.Transcript));
        diagnostics.Assert("a Server auth using line was written", false, serverAuth);
        diagnostics.Assert("problem line written", true, events.Transcript.Contains("* Basic authentication problem, ignoring."));
        Assert.IsFalse(serverAuth);
        Assert.Contains("* Basic authentication problem, ignoring.", events.Transcript);
    }

    [TestMethod]
    [DataRow(HttpAuthSchemes.Any)]
    [DataRow(HttpAuthSchemes.Digest)]
    [DataRow(HttpAuthSchemes.Ntlm)]
    public async Task ExecuteAsync_BasicChallengeNotPickedVerbose_WritesNoProblem(HttpAuthSchemes schemes)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("schemes", schemes);
        diagnostics.Bytes("scripted 401 reply", Encoding.Latin1.GetBytes(BasicDenied));

        RecordingTransferEvents events = await RunAsync(BasicDenied, new RecordingAuthenticator(), schemes);

        diagnostics.Act("events", Show(events.Transcript));
        diagnostics.Assert("problem line written", false, events.Transcript.Contains("* Basic authentication problem, ignoring."));
        Assert.DoesNotContain("* Basic authentication problem, ignoring.", events.Transcript);
    }

    [TestMethod]
    public async Task ExecuteAsync_AnyAuthVerbose_WritesNeitherLine()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("schemes", HttpAuthSchemes.Any);
        diagnostics.Bytes("scripted 401 reply", Encoding.Latin1.GetBytes(BasicDenied));

        RecordingTransferEvents events = await RunAsync(BasicDenied, new RecordingAuthenticator(), HttpAuthSchemes.Any);

        bool authLine = events.Transcript.Any(line => line.Contains("auth", StringComparison.Ordinal) && line.StartsWith("* ", StringComparison.Ordinal));
        diagnostics.Act("events", Show(events.Transcript));
        diagnostics.Assert("an auth info line was written", false, authLine);
        Assert.IsFalse(authLine);
    }

    [TestMethod]
    public async Task ExecuteAsync_DigestUpgradeAcceptedVerbose_WritesServerAuthUsingAndNoProblem()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("schemes", HttpAuthSchemes.Digest);
        diagnostics.Bytes("scripted 101 head", Encoding.Latin1.GetBytes(Head101));

        RecordingTransferEvents events = await RunAsync(Head101, new RecordingAuthenticator(), HttpAuthSchemes.Digest);

        bool problem = events.Transcript.Any(line => line.Contains("problem", StringComparison.Ordinal));
        diagnostics.Act("events", Show(events.Transcript));
        diagnostics.Assert("second event", "* Server auth using Digest with user 'u'", events.Transcript[1]);
        diagnostics.Assert("a problem line was written", false, problem);
        Assert.AreEqual("* Server auth using Digest with user 'u'", events.Transcript[1]);
        Assert.IsFalse(problem);
    }

    private static string Show(IEnumerable<string> lines) =>
        string.Join(" | ", lines).Replace("\r", "\\r", StringComparison.Ordinal).Replace("\n", "\\n", StringComparison.Ordinal);

    private static async Task<RecordingTransferEvents> RunAsync(string reply, IHttpAuthenticator authenticator, HttpAuthSchemes schemes, params string[] headers)
    {
        var events = new RecordingTransferEvents();
        var handler = new WsProtocolHandler(
            new RecordingConnector(ConnectResult.Connected(new ScriptedConnection(Encoding.Latin1.GetBytes(reply)), null, connectionNumber: 0)),
            authenticator,
            new FixedRandomSource());
        await handler.ExecuteAsync(new TransferContext
        {
            Url = CurlUrl.Parse("ws://127.0.0.1:18953/c"),
            Output = new MemoryStream(),
            Events = events,
            Credentials = new NetworkCredential("u", "p"),
            Http = new HttpRequestOptions { AuthSchemes = schemes, Headers = headers },
        });
        return events;
    }
}
