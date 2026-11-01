using Curl.Protocol.Abstractions;
using Curl.Original.Protocol.Http.Fakes;
using HttpRequestOptions = Curl.Protocol.Abstractions.HttpRequestOptions;

namespace Curl.Original.Protocol.Http;

/// <content>
/// Pins the first request of a hop on a pooled connection an earlier hop's NTLM handshake
/// authenticated: curl 8.22.0 sends it with no <c>Authorization</c> and with its body, since the
/// connection's NTLM state is past Type 3, while a new connection starts again with Type 1
/// (upstream test1100, BL-3003).
/// </content>
public sealed partial class HttpProtocolHandlerTests
{
    [TestMethod]
    public async Task ExecuteAsync_NtlmPickedOnAReusedConnection_SendsNoType1()
    {
        TurnTakingConnection connection = new(65536, OkHead + "ok");
        Diagnostics.Arrange("earlier hop picked, connection", "NTLM, reused");

        TransferResult result = await new HttpProtocolHandler(ReusedConnector(connection, isReused: true), new ScriptedAuthenticator(NtlmType1, null)).ExecuteAsync(NtlmPickedContext(null));

        WriteResult(result);
        Diagnostics.Diff("connection written", PlainRequest, connection.Written);
        Assert.AreEqual(PlainRequest, connection.Written);
    }

    [TestMethod]
    public async Task ExecuteAsync_NtlmPickedOnAReusedConnectionForAPost_SendsTheBodyWithoutType1()
    {
        TurnTakingConnection connection = new(65536, OkHead + "ok");
        Diagnostics.Arrange("earlier hop picked, connection, body", "NTLM, reused, -d hello");

        TransferResult result = await new HttpProtocolHandler(ReusedConnector(connection, isReused: true), new ScriptedAuthenticator(NtlmType1, null))
            .ExecuteAsync(NtlmPickedContext(new BytesBody("hello"u8.ToArray(), "application/x-www-form-urlencoded")));

        WriteResult(result);
        Diagnostics.Assert("exit code", CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.DoesNotContain("Authorization:", connection.Written);
        Assert.EndsWith("Content-Length: 5\r\nContent-Type: application/x-www-form-urlencoded\r\n\r\nhello", connection.Written);
    }

    [TestMethod]
    public async Task ExecuteAsync_NtlmPickedOnANewConnection_SendsType1Again()
    {
        TurnTakingConnection connection = new(65536, OkHead + "ok", OkHead + "ok");
        Diagnostics.Arrange("earlier hop picked, connection", "NTLM, new");

        TransferResult result = await new HttpProtocolHandler(ReusedConnector(connection, isReused: false), new ScriptedAuthenticator(NtlmType1, null)).ExecuteAsync(NtlmPickedContext(null));

        WriteResult(result);
        Assert.Contains("Authorization: " + NtlmType1 + "\r\n", connection.Written);
    }

    [TestMethod]
    public async Task ExecuteAsync_NtlmPickedOnAReusedConnectionWithAnotherValue_SendsThatValue()
    {
        TurnTakingConnection connection = new(65536, OkHead + "ok");
        Diagnostics.Arrange("earlier hop picked, connection, value", "NTLM, reused, Basic");

        TransferResult result = await new HttpProtocolHandler(ReusedConnector(connection, isReused: true), new ScriptedAuthenticator("Basic dTpw", null)).ExecuteAsync(NtlmPickedContext(null));

        WriteResult(result);
        Assert.Contains("Authorization: Basic dTpw\r\n", connection.Written);
    }

    [TestMethod]
    public async Task ExecuteAsync_NtlmPickedByTheHandlesEarlierTransferOnAReusedConnection_SendsType1Again()
    {
        TurnTakingConnection connection = new(65536, OkHead + "ok", OkHead + "ok");
        Diagnostics.Arrange("earlier transfer picked, connection, redirects", "NTLM, reused, 0");

        TransferResult result = await new HttpProtocolHandler(ReusedConnector(connection, isReused: true), new ScriptedAuthenticator(NtlmType1, null)).ExecuteAsync(NtlmPickedContext(null, redirectsFollowed: 0));

        WriteResult(result);
        Assert.Contains("Authorization: " + NtlmType1 + "\r\n", connection.Written);
    }

    private static QueueConnector ReusedConnector(TurnTakingConnection connection, bool isReused) =>
        new(ConnectResult.Connected(connection, null, isReused: isReused));

    private static TransferContext NtlmPickedContext(BytesBody? body, int redirectsFollowed = 1) => new()
    {
        Url = CurlUrl.Parse(AuthUrl),
        Output = new MemoryStream(),
        Credentials = new System.Net.NetworkCredential("u", "p"),
        Http = new HttpRequestOptions { Body = body, AuthSchemes = HttpAuthSchemes.Ntlm, AuthSchemePicked = HttpAuthSchemes.Ntlm, RedirectsFollowed = redirectsFollowed },
    };
}
