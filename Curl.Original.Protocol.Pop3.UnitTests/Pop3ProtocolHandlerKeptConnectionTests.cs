using System.Net;
using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Original.Protocol.Pop3.Fakes;

namespace Curl.Original.Protocol.Pop3;

/// <summary>
/// Pins how a POP3 connection outlives its transfer (BL-3167): a successful transfer leaves it
/// logged in to the run's connection cache, the next URL for the same login sends only its
/// command, <c>QUIT</c> is sent once, when the cache closes it, and a different login or a
/// failed transfer gets a connection of its own. Measured 2026-10-22 with
/// <c>Record-CurlExchange.ps1 -Pop3</c>, curl 8.22.0 (Schannel) running
/// <c>-sS -u u:p pop3://127.0.0.1:18116/1 pop3://127.0.0.1:18116/2</c>: one connection carrying
/// <c>CAPA</c>, <c>AUTH PLAIN</c>, <c>AHUAcA==</c>, <c>RETR 1</c>, <c>RETR 2</c> and <c>QUIT</c>.
/// </summary>
[TestClass]
public sealed class Pop3ProtocolHandlerKeptConnectionTests
{
    private const string Greeting = "+OK POP3 ready <1896.697170952@localhost>\r\n";

    private const string CapaReply = "+OK Capability list follows\r\nUSER\r\nSASL PLAIN LOGIN\r\nSTLS\r\nTOP\r\nUIDL\r\n.\r\n";

    private const string Login = "+ \r\n+OK Authenticated\r\n";

    private const string Message = "+OK 3 octets\r\nhi\r\n.\r\n";

    /// <summary>curl 8.22.0's measured command log for the two URLs (see the class summary).</summary>
    private const string MeasuredTwoUrlLog = "CAPA\r\nAUTH PLAIN\r\nAHUAcA==\r\nRETR 1\r\nRETR 2\r\nQUIT\r\n";

    [TestMethod]
    public async Task ExecuteAsync_TwoUrlsForTheSameLogin_LogInOnceAndQuitOnceAsCurlDoes()
    {
        var connection = new PoolingConnection(Read(Greeting), Read(CapaReply), Read(Login), Read(Message), Read(Message), Read("+OK Bye\r\n"));
        var connector = new QueuedConnector(ConnectResult.Connected(connection, null), ConnectResult.Connected(connection, null, isReused: true));
        var handler = new Pop3ProtocolHandler(connector, new QueuedTlsProvider(), Plain());

        TransferResult first = await handler.ExecuteAsync(Context("1", "u"));
        TransferResult second = await handler.ExecuteAsync(Context("2", "u"));
        bool closedBeforeExit = connection.IsClosed;
        await connection.CloseAsync();

        Assert.AreEqual(CurlExitCode.Ok, first.ExitCode);
        Assert.AreEqual(CurlExitCode.Ok, second.ExitCode);
        Assert.IsFalse(closedBeforeExit);
        Assert.AreEqual(MeasuredTwoUrlLog, connection.SentText);
        Assert.AreEqual("pop3", connector.Targets[0].PoolScheme);
    }

    [TestMethod]
    public async Task ExecuteAsync_SecondUrlForAnotherUser_QuitsTheKeptConnectionAndLogsInOnANewOne()
    {
        var first = new PoolingConnection(Read(Greeting), Read(CapaReply), Read(Login), Read(Message), Read("+OK Bye\r\n"));
        var second = new PoolingConnection(Read(Greeting), Read(CapaReply), Read(Login), Read(Message));
        var connector = new QueuedConnector(
            ConnectResult.Connected(first, null), ConnectResult.Connected(first, null, isReused: true), ConnectResult.Connected(second, null, connectionNumber: 1));
        var handler = new Pop3ProtocolHandler(connector, new QueuedTlsProvider(), Plain());

        await handler.ExecuteAsync(Context("1", "u"));
        TransferResult result = await handler.ExecuteAsync(Context("2", "v"));

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.IsTrue(first.IsClosed);
        Assert.AreEqual("CAPA\r\nAUTH PLAIN\r\nAHUAcA==\r\nRETR 1\r\nQUIT\r\n", first.SentText);
        Assert.AreEqual("CAPA\r\nAUTH PLAIN\r\nAHUAcA==\r\nRETR 2\r\n", second.SentText);
    }

    [TestMethod]
    public async Task ExecuteAsync_SecondUrlOnAFreshConnection_LogsInAgainAndLeavesTheFirstKept()
    {
        // --fresh-connect makes the connection cache open a new connection rather than serve the kept one.
        var first = new PoolingConnection(Read(Greeting), Read(CapaReply), Read(Login), Read(Message));
        var second = new PoolingConnection(Read(Greeting), Read(CapaReply), Read(Login), Read(Message));
        var connector = new QueuedConnector(ConnectResult.Connected(first, null), ConnectResult.Connected(second, null, connectionNumber: 1));
        var handler = new Pop3ProtocolHandler(connector, new QueuedTlsProvider(), Plain());

        await handler.ExecuteAsync(Context("1", "u"));
        TransferResult result = await handler.ExecuteAsync(Context("2", "u"));

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.IsFalse(first.IsClosed);
        Assert.AreEqual("CAPA\r\nAUTH PLAIN\r\nAHUAcA==\r\nRETR 1\r\n", first.SentText);
        Assert.AreEqual("CAPA\r\nAUTH PLAIN\r\nAHUAcA==\r\nRETR 2\r\n", second.SentText);
    }

    [TestMethod]
    public async Task ExecuteAsync_FirstTransferFails_QuitsAndClosesSoTheSecondUrlLogsInOnANewConnection()
    {
        var first = new PoolingConnection(Read(Greeting), Read(CapaReply), Read(Login), Read("-ERR no such message\r\n"), Read("+OK Bye\r\n"));
        var second = new PoolingConnection(Read(Greeting), Read(CapaReply), Read(Login), Read(Message));
        var connector = new QueuedConnector(ConnectResult.Connected(first, null), ConnectResult.Connected(second, null, connectionNumber: 1));
        var handler = new Pop3ProtocolHandler(connector, new QueuedTlsProvider(), Plain());

        TransferResult failed = await handler.ExecuteAsync(Context("1", "u"));
        TransferResult result = await handler.ExecuteAsync(Context("2", "u"));

        Assert.AreNotEqual(CurlExitCode.Ok, failed.ExitCode);
        Assert.IsTrue(first.IsClosed);
        Assert.IsNull(first.Session);
        Assert.AreEqual("CAPA\r\nAUTH PLAIN\r\nAHUAcA==\r\nRETR 1\r\nQUIT\r\n", first.SentText);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual("CAPA\r\nAUTH PLAIN\r\nAHUAcA==\r\nRETR 2\r\n", second.SentText);
    }

    [TestMethod]
    public async Task ExecuteAsync_TransferOnAKeptConnectionFails_QuitsThenAndNotAgainWhenClosed()
    {
        var connection = new PoolingConnection(Read(Greeting), Read(CapaReply), Read(Login), Read(Message), Read("-ERR no such message\r\n"), Read("+OK Bye\r\n"));
        var connector = new QueuedConnector(ConnectResult.Connected(connection, null), ConnectResult.Connected(connection, null, isReused: true));
        var handler = new Pop3ProtocolHandler(connector, new QueuedTlsProvider(), Plain());

        await handler.ExecuteAsync(Context("1", "u"));
        TransferResult failed = await handler.ExecuteAsync(Context("9", "u"));

        Assert.AreNotEqual(CurlExitCode.Ok, failed.ExitCode);
        Assert.IsTrue(connection.IsClosed);
        Assert.AreEqual("CAPA\r\nAUTH PLAIN\r\nAHUAcA==\r\nRETR 1\r\nRETR 9\r\nQUIT\r\n", connection.SentText);
    }

    [TestMethod]
    public async Task ShutDownAsync_ServerHangsUpBeforeReplying_EndsQuietly()
    {
        var connection = new ScriptedConnection();
        var kept = new Pop3KeptConnection(connection, string.Empty, TimeProvider.System) { QuitsOnShutDown = true };

        await kept.ShutDownAsync(CancellationToken.None);

        Assert.AreEqual("QUIT\r\n", Encoding.ASCII.GetString(connection.Sent));
    }

    [TestMethod]
    public async Task ShutDownAsync_ReplyWithoutALineEnd_StopsAfter64KiB()
    {
        var connection = new ScriptedConnection(new byte[64 * 1024], Read("+OK\r\n"));
        var kept = new Pop3KeptConnection(connection, string.Empty, TimeProvider.System) { QuitsOnShutDown = true };

        await kept.ShutDownAsync(CancellationToken.None);

        Assert.AreEqual(1, connection.ReadCount);
    }

    [TestMethod]
    public async Task ShutDownAsync_QuitCannotBeSent_EndsQuietly()
    {
        var connection = new ScriptedConnection { WritesBeforeFailure = 0 };
        var kept = new Pop3KeptConnection(connection, string.Empty, TimeProvider.System) { QuitsOnShutDown = true };

        await kept.ShutDownAsync(CancellationToken.None);

        Assert.IsEmpty(connection.Sent);
    }

    [TestMethod]
    public async Task ShutDownAsync_WaitCancelled_EndsQuietly()
    {
        var connection = new ScriptedConnection(Read("+OK\r\n")) { WritesBeforeFailure = 0, WriteFailure = new OperationCanceledException() };
        var kept = new Pop3KeptConnection(connection, string.Empty, TimeProvider.System) { QuitsOnShutDown = true };

        await kept.ShutDownAsync(CancellationToken.None);

        Assert.AreEqual(0, connection.ReadCount);
    }

    private static ScriptedSaslAuthenticator Plain() => new(("PLAIN", ["\0u\0p"]));

    private static TransferContext Context(string message, string user) => new()
    {
        Url = CurlUrl.Parse("pop3://127.0.0.1:18116/" + message),
        Output = Stream.Null,
        Credentials = new NetworkCredential(user, "p"),
    };

    private static byte[] Read(string text) => Encoding.Latin1.GetBytes(text);

    /// <summary>
    /// A scripted connection the run's connection cache would pool: it holds a session, and
    /// disposing it after <see cref="MarkReusable" /> keeps it open, as the cache does.
    /// </summary>
    private sealed class PoolingConnection(params byte[][] reads) : IConnection
    {
        private readonly ScriptedConnection inner = new(reads);

        private bool markedReusable;

        public string SentText => Encoding.Latin1.GetString(inner.Sent);

        public bool IsClosed { get; private set; }

        public IConnectionSession? Session { get; private set; }

        public bool IsSecure => false;

        public EndPoint? RemoteEndPoint => null;

        public bool TryHoldSession(IConnectionSession session)
        {
            Session = session;
            return true;
        }

        public void MarkReusable() => markedReusable = true;

        public ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken) => inner.ReadAsync(buffer, cancellationToken);

        public ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken) => inner.WriteAsync(buffer, cancellationToken);

        public ValueTask FlushAsync(CancellationToken cancellationToken) => ValueTask.CompletedTask;

        public async ValueTask CloseAsync()
        {
            if (Session is not null)
            {
                await Session.ShutDownAsync(CancellationToken.None);
            }

            Session = null;
            IsClosed = true;
        }

        public async ValueTask DisposeAsync()
        {
            if (markedReusable)
            {
                markedReusable = false;
                return;
            }

            await CloseAsync();
        }
    }
}
