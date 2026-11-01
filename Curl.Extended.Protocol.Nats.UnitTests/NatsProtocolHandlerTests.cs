using System.Net;
using System.Text;
using Curl.Protocol.Abstractions;

namespace Curl.Extended.Protocol.Nats;

/// <summary>
/// Drives the handler through a scripted server, checking each flow against the NATS client protocol
/// ("INFO", "CONNECT", "PUB", "SUB", "UNSUB", "MSG", "PING/PONG", "+OK/ERR").
/// </summary>
[TestClass]
public sealed class NatsProtocolHandlerTests
{
    private const string Info = "INFO {\"server_id\":\"x\",\"max_payload\":8}\r\n";

    private const string Connect = "CONNECT {\"verbose\":false,\"pedantic\":false,\"tls_required\":false,\"name\":\"curl\",\"lang\":\"curl\",\"version\":\"1.0.0\",\"protocol\":1}\r\n";

    [TestMethod]
    public async Task ExecuteAsync_WithData_PublishesAndEndsOnThePong()
    {
        ScriptedConnection connection = ScriptedConnection.Of(Info, "+OK\r\nINFO {}\r\n", "PONG\r\n", "MSG never 1 0\r\n\r\n");
        MemoryStream output = new();

        TransferResult result = await ExecuteAsync(connection, "nats://example.com/orders.new", output, "hello");

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(0, output.Length);
        Assert.AreEqual(Connect + "PUB orders.new 5\r\nhello\r\nPING\r\n", connection.Sent);
        Assert.IsTrue(connection.IsDisposed);
    }

    [TestMethod]
    public async Task ExecuteAsync_WithoutData_SubscribesAnswersPingsAndWritesEachPayloadUntilClosed()
    {
        ScriptedConnection connection = ScriptedConnection.Of(Info, "PONG\r\nMSG a.b 1 2\r\nhi\r\n", "ping\r\n", "MSG a.b 1 _INBOX.r 3\r\nyo!\r\n");
        MemoryStream output = new();

        TransferResult result = await ExecuteAsync(connection, "nats://example.com/a.b", output);

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual("hi\nyo!\n", Encoding.UTF8.GetString(output.ToArray()));
        Assert.AreEqual(7, result.BytesTransferred);
        Assert.AreEqual(Connect + "SUB a.b 1\r\nPING\r\nPONG\r\n", connection.Sent);
    }

    [TestMethod]
    public async Task ExecuteAsync_ACount_UnsubscribesAfterThatManyAndEndsThere()
    {
        ScriptedConnection connection = ScriptedConnection.Of(Info, "MSG a 1 1\r\nx\r\nMSG a 1 1\r\ny\r\nMSG a 1 1\r\nz\r\n");
        MemoryStream output = new();

        TransferResult result = await ExecuteAsync(connection, "nats://example.com/a?count=2", output);

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual("x\ny\n", Encoding.UTF8.GetString(output.ToArray()));
        Assert.AreEqual(Connect + "SUB a 1\r\nUNSUB 1 2\r\nPING\r\n", connection.Sent);
    }

    [TestMethod]
    public async Task ExecuteAsync_ARequest_SubscribesAnInboxPublishesWithItAndWritesTheReply()
    {
        ScriptedConnection connection = ScriptedConnection.Of(Info, "PONG\r\n", "MSG _INBOX.t 1 4\r\npong\r\n");
        MemoryStream output = new();

        TransferResult result = await ExecuteAsync(connection, "nats://example.com/svc.echo?request", output, "ping");

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual("pong", Encoding.UTF8.GetString(output.ToArray()));
        Assert.AreEqual(Connect + "SUB _INBOX.t 1\r\nUNSUB 1 1\r\nPUB svc.echo _INBOX.t 4\r\nping\r\nPING\r\n", connection.Sent);
    }

    [TestMethod]
    public async Task ExecuteAsync_ARequestWithoutData_PublishesAnEmptyPayload()
    {
        ScriptedConnection connection = ScriptedConnection.Of(Info, "MSG _INBOX.t 1 0\r\n\r\n");

        TransferResult result = await ExecuteAsync(connection, "nats://example.com/svc?count=3&request", new MemoryStream());

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        StringAssert.Contains(connection.Sent, "PUB svc _INBOX.t 0\r\n\r\n");
    }

    [TestMethod]
    [DataRow("nats://example.com/svc?request", "x")]
    [DataRow("nats://example.com/svc", "x")]
    public async Task ExecuteAsync_ClosedBeforeTheReply_FailsWithExit52(string url, string data)
    {
        TransferResult result = await ExecuteAsync(ScriptedConnection.Of(Info, "+OK\r\n"), url, new MemoryStream(), data);

        Assert.AreEqual(CurlExitCode.GotNothing, result.ExitCode);
        Assert.AreEqual(NatsMessages.ClosedBeforeReply, result.ErrorMessage);
    }

    [TestMethod]
    [DataRow("-ERR 'Authorization Violation'", CurlExitCode.LoginDenied, "NATS server refused the login: Authorization Violation")]
    [DataRow("-ERR 'Permissions Violation for Publish to a'", CurlExitCode.WeirdServerReply, "NATS server error: Permissions Violation for Publish to a")]
    [DataRow("HELLO there", CurlExitCode.WeirdServerReply, "NATS server sent an unexpected line: HELLO there")]
    public async Task ExecuteAsync_AnErrorOrUnknownLine_FailsWithItsExitCode(string line, CurlExitCode exitCode, string message)
    {
        TransferResult result = await ExecuteAsync(ScriptedConnection.Of(Info, line + "\r\n"), "nats://al:pw@example.com/a", new MemoryStream(), "x");

        Assert.AreEqual(exitCode, result.ExitCode);
        Assert.AreEqual(message, result.ErrorMessage);
    }

    [TestMethod]
    public async Task ExecuteAsync_DataOverMaxPayload_FailsWithExit63BeforePublishing()
    {
        ScriptedConnection connection = ScriptedConnection.Of(Info);

        TransferResult result = await ExecuteAsync(connection, "nats://example.com/a", new MemoryStream(), "123456789");

        Assert.AreEqual(CurlExitCode.FilesizeExceeded, result.ExitCode);
        Assert.AreEqual("NATS payload of 9 bytes is larger than the server's max_payload of 8", result.ErrorMessage);
        Assert.AreEqual(string.Empty, connection.Sent);
    }

    [TestMethod]
    public async Task ExecuteAsync_PlainUrlToAServerRequiringTls_FailsWithExit35()
    {
        TransferResult result = await ExecuteAsync(ScriptedConnection.Of("INFO {\"tls_required\":true}\r\n"), "nats://example.com/a", new MemoryStream());

        Assert.AreEqual(CurlExitCode.SslConnectError, result.ExitCode);
        Assert.AreEqual(NatsMessages.TlsRequired, result.ErrorMessage);
    }

    [TestMethod]
    public async Task ExecuteAsync_TlsUrlToAServerRequiringTls_ConnectsWithTlsRequiredAndTheCredentials()
    {
        ScriptedConnection connection = ScriptedConnection.Of("INFO {\"tls_required\":true}\r\n", "PONG\r\n");
        NatsProtocolHandler handler = new(new ScriptedConnector(ConnectResult.Connected(connection)), () => "_INBOX.t");

        TransferResult result = await handler.ExecuteAsync(new TransferContext { Url = CurlUrl.Parse("natss://example.com/a"), Output = new MemoryStream(), PostData = "x"u8.ToArray(), Credentials = new NetworkCredential("s3cr3t", string.Empty) });

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        StringAssert.StartsWith(connection.Sent, "CONNECT {\"verbose\":false,\"pedantic\":false,\"tls_required\":true,\"name\":\"curl\",\"lang\":\"curl\",\"version\":\"1.0.0\",\"protocol\":1,\"auth_token\":\"s3cr3t\"}\r\n");
    }

    [TestMethod]
    public async Task ExecuteAsync_ClosedBeforeInfo_FailsWithExit8()
    {
        TransferResult result = await ExecuteAsync(ScriptedConnection.Of(), "nats://example.com/a", new MemoryStream());

        Assert.AreEqual(CurlExitCode.WeirdServerReply, result.ExitCode);
        Assert.AreEqual(NatsMessages.NoInfo, result.ErrorMessage);
    }

    [TestMethod]
    [DataRow("nats://example.com/a?count=0")]
    [DataRow("nats://example.com/a?count=x")]
    [DataRow("nats://example.com/a?queue=q")]
    public async Task ExecuteAsync_AQueryFieldItDoesNotKnow_FailsWithExit3BeforeConnecting(string url)
    {
        ScriptedConnector connector = new(ConnectResult.Connected(ScriptedConnection.Of()));

        TransferResult result = await new NatsProtocolHandler(connector, () => "_INBOX.t").ExecuteAsync(Context(url, new MemoryStream()));

        Assert.AreEqual(CurlExitCode.UrlMalformat, result.ExitCode);
        StringAssert.EndsWith(result.ErrorMessage, "\" is neither request nor count=N");
        Assert.IsNull(connector.Target);
    }

    [TestMethod]
    public async Task ExecuteAsync_ConnectionRefused_GivesTheConnectorsFailure()
    {
        TransferResult result = await new NatsProtocolHandler(new ScriptedConnector(ConnectResult.Refused("Failed to connect")), () => "_INBOX.t").ExecuteAsync(Context("nats://example.com/a", new MemoryStream()));

        Assert.AreEqual(CurlExitCode.CouldntConnect, result.ExitCode);
        Assert.IsTrue(result.IsConnectionRefused);
    }

    [TestMethod]
    [DataRow("nats://example.com/a", 4222, false)]
    [DataRow("NATSS://example.com/a", 4222, true)]
    [DataRow("natss://example.com:4443/a", 4443, true)]
    public async Task ExecuteAsync_TheScheme_ChoosesTheDefaultPortAndTls(string url, int port, bool useTls)
    {
        ScriptedConnector connector = new(ConnectResult.Refused("x"));

        await new NatsProtocolHandler(connector, () => "_INBOX.t").ExecuteAsync(Context(url, new MemoryStream()));

        Assert.AreEqual(new ConnectTarget("example.com", port, useTls), connector.Target! with { Events = NoTransferEvents.Instance, DiagnosticLog = NoDiagnosticLog.Instance });
    }

    [TestMethod]
    public async Task ExecuteAsync_WithoutAContext_Throws() =>
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(async () => await new NatsProtocolHandler(new ScriptedConnector(ConnectResult.Refused("x")), () => "_INBOX.t").ExecuteAsync(null!));

    [TestMethod]
    public void SupportedSchemes_AreNatsAndNatss() =>
        CollectionAssert.AreEqual(new[] { "nats", "natss" }, new NatsProtocolHandler(new ScriptedConnector(ConnectResult.Refused("x")), () => "_INBOX.t").SupportedSchemes.ToArray());

    [TestMethod]
    public void NewInbox_IsInboxPrefixedAndFresh()
    {
        string first = NatsProtocolHandler.NewInbox();

        Assert.MatchesRegex("^_INBOX\\.[0-9a-f]{32}$", first);
        Assert.AreNotEqual(first, NatsProtocolHandler.NewInbox());
    }

    private static ValueTask<TransferResult> ExecuteAsync(ScriptedConnection connection, string url, Stream output, string? data = null) =>
        new NatsProtocolHandler(new ScriptedConnector(ConnectResult.Connected(connection)), () => "_INBOX.t").ExecuteAsync(Context(url, output, data));

    private static TransferContext Context(string url, Stream output, string? data = null) =>
        new() { Url = CurlUrl.Parse(url), Output = output, PostData = data is null ? null : (ReadOnlyMemory<byte>?)Encoding.UTF8.GetBytes(data) };
}
