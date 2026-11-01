using System.Net;
using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Original.Protocol.Imap.Fakes;
using Curl.Testing;

namespace Curl.Original.Protocol.Imap;

/// <summary>
/// Pins <see cref="ImapProtocolHandler.ConnectOnlyAsync" />, libcurl 8.22.0's
/// <c>CURLOPT_CONNECT_ONLY</c> 1 for <c>imap://</c> (BL-2706): upstream test 660 expects exactly
/// <c>A001 CAPABILITY</c> on the wire with no credentials, and the connection is handed back
/// open with no mailbox selected and no <c>LOGOUT</c>.
/// </summary>
[TestClass]
public sealed class ImapProtocolHandlerConnectOnlyTests
{
    private const string Url = "imap://127.0.0.1:18143/660";

    private const string Greeting = "* OK ready\r\n";

    private const string Capability = "A001 CAPABILITY\r\n";

    private const string CapabilityReply = "* CAPABILITY IMAP4rev1 STARTTLS\r\nA001 OK CAPABILITY completed\r\n";

    /// <summary>Gets or sets the running test's context, which carries its diagnostics.</summary>
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public async Task ConnectOnlyAsync_NoCredentials_SendsOnlyCapabilityAndKeepsTheConnectionOpen()
    {
        var connection = new ScriptedConnection(Latin1(Greeting + CapabilityReply));

        (ImapConnectOutcome outcome, _) = await ConnectOnlyAsync(Context(), connection);

        Diagnostics.Diff("sent", Capability, Latin1(connection.Sent));
        Assert.AreEqual(Capability, Latin1(connection.Sent));
        Assert.AreEqual(TransferResult.Success(0), outcome.Result);
        Assert.AreSame(connection, outcome.Connection);
        Assert.IsFalse(connection.IsDisposed);
    }

    [TestMethod]
    public async Task ConnectOnlyAsync_Credentials_SendsLoginAfterCapabilityAndNothingMore()
    {
        var connection = new ScriptedConnection(Latin1(Greeting + CapabilityReply + "A002 OK LOGIN completed\r\n"));
        TransferContext context = Context(new NetworkCredential("user", "secret"));

        (ImapConnectOutcome outcome, _) = await ConnectOnlyAsync(context, connection);

        Diagnostics.Diff("sent", Capability + "A002 LOGIN user secret\r\n", Latin1(connection.Sent));
        Assert.AreEqual(Capability + "A002 LOGIN user secret\r\n", Latin1(connection.Sent));
        Assert.AreEqual(TransferResult.Success(0), outcome.Result);
        Assert.AreSame(connection, outcome.Connection);
        Assert.IsFalse(connection.IsDisposed);
    }

    [TestMethod]
    public async Task ConnectOnlyAsync_StartTlsRequired_HandsBackTheSecuredConnection()
    {
        var plaintext = new ScriptedConnection(Latin1(Greeting + CapabilityReply + "A002 OK Begin TLS negotiation now\r\n"));
        var secured = new ScriptedConnection(Latin1("* CAPABILITY IMAP4rev1\r\nA003 OK CAPABILITY completed\r\n"));
        TransferContext context = Context(sslLevel: TransportSecurityLevel.Required);

        (ImapConnectOutcome outcome, _) = await ConnectOnlyAsync(context, plaintext, ConnectResult.Connected(secured));

        Assert.AreEqual(Capability + "A002 STARTTLS\r\n", Latin1(plaintext.Sent));
        Assert.AreEqual("A003 CAPABILITY\r\n", Latin1(secured.Sent));
        Assert.AreEqual(TransferResult.Success(0), outcome.Result);
        Assert.AreSame(secured, outcome.Connection);
        Assert.IsFalse(secured.IsDisposed);
    }

    [TestMethod]
    public async Task ConnectOnlyAsync_UnexpectedGreeting_FailsWithExit8AndClosesTheConnection()
    {
        var connection = new ScriptedConnection(Latin1("* NO go away\r\n"));

        (ImapConnectOutcome outcome, _) = await ConnectOnlyAsync(Context(), connection);

        Assert.AreEqual(TransferResult.Failure(CurlExitCode.WeirdServerReply, ImapSessionMessages.UnexpectedGreeting), outcome.Result);
        Assert.IsNull(outcome.Connection);
        Assert.IsTrue(connection.IsDisposed);
        Assert.IsEmpty(connection.Sent);
    }

    [TestMethod]
    public async Task ConnectOnlyAsync_ConnectFails_ReturnsTheConnectorsFailure()
    {
        var handler = new ImapProtocolHandler(new QueuedConnector(ConnectResult.Refused("Failed to connect")), new QueuedTlsProvider());

        ImapConnectOutcome outcome = await handler.ConnectOnlyAsync(Context());

        Assert.AreEqual(CurlExitCode.CouldntConnect, outcome.Result.ExitCode);
        Assert.IsTrue(outcome.Result.IsConnectionRefused);
        Assert.IsNull(outcome.Connection);
    }

    [TestMethod]
    [DataRow("us\ner", "secret", DisplayName = "user")]
    [DataRow("user", "sec\rret", DisplayName = "password")]
    public async Task ConnectOnlyAsync_ControlByteInCredentials_FailsWithExit43BeforeConnecting(string user, string password)
    {
        var connection = new ScriptedConnection(Latin1(Greeting + CapabilityReply));
        TransferContext context = Context(new NetworkCredential(user, password));

        (ImapConnectOutcome outcome, QueuedConnector connector) = await ConnectOnlyAsync(context, connection);

        Assert.AreEqual(TransferResult.Failure(CurlExitCode.BadFunctionArgument, ImapSessionMessages.CredentialsHaveControlCode), outcome.Result);
        Assert.IsNull(outcome.Connection);
        Assert.IsEmpty(connector.Targets);
    }

    [TestMethod]
    public async Task ConnectOnlyAsync_NullContext_Throws()
    {
        var handler = new ImapProtocolHandler(new QueuedConnector(), new QueuedTlsProvider());

        await Assert.ThrowsExactlyAsync<ArgumentNullException>(async () => await handler.ConnectOnlyAsync(null!));
    }

    private static TransferContext Context(NetworkCredential? credentials = null, TransportSecurityLevel sslLevel = TransportSecurityLevel.None) =>
        new() { Url = CurlUrl.Parse(Url), Output = Stream.Null, Credentials = credentials, SslLevel = sslLevel };

    private async Task<(ImapConnectOutcome Outcome, QueuedConnector Connector)> ConnectOnlyAsync(
        TransferContext context, ScriptedConnection connection, params ConnectResult[] handshakes)
    {
        var connector = new QueuedConnector(ConnectResult.Connected(connection));
        var handler = new ImapProtocolHandler(connector, new QueuedTlsProvider(handshakes));
        Diagnostics.Arrange("url", context.Url.ToString());

        ImapConnectOutcome outcome = await handler.ConnectOnlyAsync(context);
        Diagnostics.Act("result", DiagnosticText.Result(outcome.Result));
        Diagnostics.Act("sent", DiagnosticText.Escape(Latin1(connection.Sent)));
        return (outcome, connector);
    }

    private static byte[] Latin1(string text) => Encoding.Latin1.GetBytes(text);

    private static string Latin1(byte[] bytes) => Encoding.Latin1.GetString(bytes);
}
