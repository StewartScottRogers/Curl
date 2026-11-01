using Curl.Protocol.Abstractions;
using Curl.Original.Protocol.Http.Fakes;
using HttpRequestOptions = Curl.Protocol.Abstractions.HttpRequestOptions;

namespace Curl.Original.Protocol.Http;

/// <content>
/// Pins a 401 that carries the NTLM Type 2 challenge on a connection it closes, as an HTTP/1.0
/// server or <c>Connection: close</c> does: curl 8.22.0 writes
/// <c>Connection closure while negotiating auth (HTTP 1.0?)</c> before the head's empty line and
/// sends no Type 3, so the 401 ends the transfer (measured with <c>-v -u testuser:testpass --ntlm -0</c>,
/// upstream test159, BL-3006).
/// </content>
public sealed partial class HttpProtocolHandlerTests
{
    private const string ClosingNtlmChallenge = "HTTP/1.1 401 Unauthorized\r\nWWW-Authenticate: NTLM TlRMTVNTUAACAAAA\r\nContent-Length: 4\r\nConnection: close\r\n\r\nnope";

    private const string Http10NtlmChallenge = "HTTP/1.0 401 Unauthorized\r\nWWW-Authenticate: NTLM TlRMTVNTUAACAAAA\r\nContent-Length: 4\r\n\r\nnope";

    [TestMethod]
    public async Task ExecuteAsync_NtlmType2OnAClosingConnection_IsNotRetried()
    {
        TurnTakingConnection connection = new(65536, ClosingNtlmChallenge, OkHead + "ok");
        MemoryStream output = new();
        Diagnostics.Arrange("responses", "401 NTLM Type 2 with Connection: close, 200");

        TransferResult result = await new HttpProtocolHandler(QueueConnector.For(connection), new ScriptedAuthenticator(NtlmType1, NtlmType3)).ExecuteAsync(NtlmClosureContext(output, new RecordingTransferEvents()));

        WriteResult(result);
        Diagnostics.Assert("request heads", 1, CountOf("GET /a", connection.Written));
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(1, CountOf("GET /a", connection.Written));
        Assert.DoesNotContain(NtlmType3, connection.Written);
        Assert.AreEqual("nope", System.Text.Encoding.ASCII.GetString(output.ToArray()));
    }

    [TestMethod]
    public async Task ExecuteAsync_NtlmType2FromAnHttp10Server_IsNotRetried()
    {
        TurnTakingConnection connection = new(65536, Http10NtlmChallenge, OkHead + "ok");
        Diagnostics.Arrange("responses", "HTTP/1.0 401 NTLM Type 2, 200");

        TransferResult result = await new HttpProtocolHandler(QueueConnector.For(connection), new ScriptedAuthenticator(NtlmType1, NtlmType3)).ExecuteAsync(NtlmClosureContext(new MemoryStream(), new RecordingTransferEvents()));

        WriteResult(result);
        Diagnostics.Assert("request heads", 1, CountOf("GET /a", connection.Written));
        Assert.AreEqual(1, CountOf("GET /a", connection.Written));
    }

    [TestMethod]
    public async Task ExecuteAsync_NtlmType2OnAClosingConnectionVerbose_WritesTheClosureLineBeforeTheEmptyLine()
    {
        RecordingTransferEvents events = new();
        Diagnostics.Arrange("responses", "401 NTLM Type 2 with Connection: close");

        await new HttpProtocolHandler(QueueConnector.For(new TurnTakingConnection(65536, ClosingNtlmChallenge)), new ScriptedAuthenticator(NtlmType1, NtlmType3)).ExecuteAsync(NtlmClosureContext(new MemoryStream(), events));

        List<string> lines = FirstLinesOf(events);
        WriteEvents("events", lines);
        int closure = lines.IndexOf("* Connection closure while negotiating auth (HTTP 1.0?)");
        Diagnostics.Assert("lines around the closure line", "< Connection: close | * Connection closure while negotiating auth (HTTP 1.0?) | < ", closure < 1 ? "(missing)" : string.Join(" | ", lines[(closure - 1)..(closure + 2)]));
        CollectionAssert.AreEqual(new[] { "< Connection: close", "* Connection closure while negotiating auth (HTTP 1.0?)", "< " }, lines[(closure - 1)..(closure + 2)]);
    }

    [TestMethod]
    [DataRow("HTTP/1.1 401 Unauthorized\r\nWWW-Authenticate: NTLM TlRMTVNTUAACAAAA\r\nContent-Length: 4\r\n\r\nnope", DisplayName = "Type 2 on a kept connection")]
    [DataRow("HTTP/1.1 401 Unauthorized\r\nWWW-Authenticate: NTLM\r\nContent-Length: 4\r\nConnection: close\r\n\r\nnope", DisplayName = "bare NTLM on a closing connection")]
    [DataRow("HTTP/1.1 403 Forbidden\r\nWWW-Authenticate: NTLM TlRMTVNTUAACAAAA\r\nContent-Length: 4\r\nConnection: close\r\n\r\nnope", DisplayName = "403 on a closing connection")]
    public async Task ExecuteAsync_NoNtlmType2On401ThatCloses_WritesNoClosureLine(string response)
    {
        RecordingTransferEvents events = new();
        Diagnostics.Arrange("response", response);

        await new HttpProtocolHandler(QueueConnector.For(new TurnTakingConnection(65536, response)), new ScriptedAuthenticator(NtlmType1, null)).ExecuteAsync(NtlmClosureContext(new MemoryStream(), events));

        List<string> lines = FirstLinesOf(events);
        WriteEvents("events", lines);
        Assert.DoesNotContain("* Connection closure while negotiating auth (HTTP 1.0?)", lines);
    }

    [TestMethod]
    public async Task ExecuteAsync_BasicOn401ThatCarriesNtlmType2AndCloses_WritesNoClosureLine()
    {
        RecordingTransferEvents events = new();
        Diagnostics.Arrange("sent, response", "Basic, 401 NTLM Type 2 with Connection: close");

        await new HttpProtocolHandler(QueueConnector.For(new TurnTakingConnection(65536, ClosingNtlmChallenge)), new ScriptedAuthenticator("Basic dTpw", null)).ExecuteAsync(NtlmClosureContext(new MemoryStream(), events));

        List<string> lines = FirstLinesOf(events);
        WriteEvents("events", lines);
        Assert.DoesNotContain("* Connection closure while negotiating auth (HTTP 1.0?)", lines);
    }

    private static int CountOf(string value, string written) =>
        written.Split(value).Length - 1;

    private static TransferContext NtlmClosureContext(Stream output, ITransferEvents events) => new()
    {
        Url = CurlUrl.Parse(AuthUrl),
        Output = output,
        Credentials = new System.Net.NetworkCredential("u", "p"),
        Http = new HttpRequestOptions { AuthSchemes = HttpAuthSchemes.Ntlm },
        Events = events,
    };
}
