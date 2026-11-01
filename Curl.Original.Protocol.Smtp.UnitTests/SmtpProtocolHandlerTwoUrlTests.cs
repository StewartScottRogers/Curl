using System.Net;
using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Original.Protocol.Smtp.Fakes;

namespace Curl.Original.Protocol.Smtp;

/// <summary>
/// Pins the command log of two uploads to one SMTP server against curl 8.22.0 (BL-3167), measured
/// 2026-10-22 with <c>Record-CurlExchange.ps1 -Smtp</c>, curl (Schannel) running
/// <c>-sS --mail-from a@x --mail-rcpt b@x -T mail.txt smtp://127.0.0.1:18118/ -T mail.txt smtp://127.0.0.1:18118/</c>:
/// one connection carrying <c>EHLO</c>, both envelopes and one <c>QUIT</c>. Curl opens and quits a
/// connection per URL before BL-3181; now it keeps one for the same login.
/// </summary>
[TestClass]
public sealed class SmtpProtocolHandlerTwoUrlTests
{
    /// <summary>curl 8.22.0's measured command log for the two uploads (see the class summary).</summary>
    private const string MeasuredTwoUrlLog =
        "EHLO mail.txt\r\nMAIL FROM:<a@x> SIZE=3\r\nRCPT TO:<b@x>\r\nDATA\r\nx\r\n.\r\n" +
        "MAIL FROM:<a@x> SIZE=3\r\nRCPT TO:<b@x>\r\nDATA\r\nx\r\n.\r\nQUIT\r\n";

    [TestMethod]
    public async Task ExecuteAsync_TwoUploadsToTheSameServer_SendEhloAndQuitOnceAsCurlDoes()
    {
        var connection = new PoolingConnection(
            Read("220 localhost ESMTP\r\n"), Read("250-localhost\r\n250 SIZE\r\n"), Read("250 OK\r\n"), Read("250 OK\r\n"), Read("354 Go\r\n"), Read("250 OK\r\n"),
            Read("250 OK\r\n"), Read("250 OK\r\n"), Read("354 Go\r\n"), Read("250 OK\r\n"), Read("221 Bye\r\n"));
        var connector = new QueuedConnector(ConnectResult.Connected(connection, null), ConnectResult.Connected(connection, null, isReused: true));
        var handler = new SmtpProtocolHandler(connector, new QueuedTlsProvider());

        TransferResult first = await handler.ExecuteAsync(Context());
        TransferResult second = await handler.ExecuteAsync(Context());
        await connection.CloseAsync();

        Assert.AreEqual(CurlExitCode.Ok, first.ExitCode);
        Assert.AreEqual(CurlExitCode.Ok, second.ExitCode);
        Assert.AreEqual(MeasuredTwoUrlLog, connection.SentText);
    }

    [TestMethod]
    public async Task ExecuteAsync_FirstUploadFails_QuitsAndKeepsNoConnection()
    {
        var connection = new PoolingConnection(
            Read("220 localhost ESMTP\r\n"), Read("250 localhost\r\n"), Read("550 No\r\n"), Read("221 Bye\r\n"));
        var connector = new QueuedConnector(ConnectResult.Connected(connection, null));
        var handler = new SmtpProtocolHandler(connector, new QueuedTlsProvider());

        TransferResult failed = await handler.ExecuteAsync(Context());

        Assert.AreNotEqual(CurlExitCode.Ok, failed.ExitCode);
        Assert.IsTrue(connection.IsClosed);
        Assert.IsNull(connection.Session);
        Assert.AreEqual("EHLO mail.txt\r\nMAIL FROM:<a@x>\r\nQUIT\r\n", connection.SentText);
    }

    [TestMethod]
    public async Task ExecuteAsync_ReusedConnectionHoldingNoSmtpSession_GreetsAgain()
    {
        var connection = new PoolingConnection(
            Read("220 localhost ESMTP\r\n"), Read("250 localhost\r\n"), Read("250 OK\r\n"), Read("250 OK\r\n"), Read("354 Go\r\n"), Read("250 OK\r\n"));
        var connector = new QueuedConnector(ConnectResult.Connected(connection, null, isReused: true));
        var handler = new SmtpProtocolHandler(connector, new QueuedTlsProvider());

        TransferResult result = await handler.ExecuteAsync(Context());

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(Envelope, connection.SentText);
        Assert.IsInstanceOfType<SmtpKeptConnection>(connection.Session);
    }

    [TestMethod]
    public async Task ExecuteAsync_UploadOnAKeptConnectionFails_QuitsThenAndNotAgainWhenClosed()
    {
        var connection = new PoolingConnection(
            Read("220 localhost ESMTP\r\n"), Read("250 localhost\r\n"), Read("250 OK\r\n"), Read("250 OK\r\n"), Read("354 Go\r\n"), Read("250 OK\r\n"),
            Read("550 No\r\n"), Read("221 Bye\r\n"));
        var connector = new QueuedConnector(ConnectResult.Connected(connection, null), ConnectResult.Connected(connection, null, isReused: true));
        var handler = new SmtpProtocolHandler(connector, new QueuedTlsProvider());

        await handler.ExecuteAsync(Context());
        TransferResult failed = await handler.ExecuteAsync(Context());

        Assert.AreNotEqual(CurlExitCode.Ok, failed.ExitCode);
        Assert.IsTrue(connection.IsClosed);
        Assert.AreEqual(Envelope + "MAIL FROM:<a@x>\r\nQUIT\r\n", connection.SentText);
    }

    [TestMethod]
    public async Task ShutDownAsync_ServerHangsUpBeforeReplying_EndsQuietly()
    {
        var connection = new ScriptedConnection();
        var kept = new SmtpKeptConnection(connection, string.Empty, TimeProvider.System) { QuitsOnShutDown = true };

        await kept.ShutDownAsync(CancellationToken.None);

        Assert.AreEqual("QUIT\r\n", Encoding.ASCII.GetString(connection.Sent));
    }

    [TestMethod]
    public async Task ShutDownAsync_ReplyWithoutALineEnd_StopsAfter64KiB()
    {
        var connection = new ScriptedConnection(new byte[64 * 1024], Read("221 Bye\r\n"));
        var kept = new SmtpKeptConnection(connection, string.Empty, TimeProvider.System) { QuitsOnShutDown = true };

        await kept.ShutDownAsync(CancellationToken.None);

        Assert.AreEqual(1, connection.ReadCount);
    }

    [TestMethod]
    public async Task ShutDownAsync_QuitCannotBeSent_EndsQuietly()
    {
        var connection = new ScriptedConnection { WritesBeforeFailure = 0 };
        var kept = new SmtpKeptConnection(connection, string.Empty, TimeProvider.System) { QuitsOnShutDown = true };

        await kept.ShutDownAsync(CancellationToken.None);

        Assert.IsEmpty(connection.Sent);
    }

    [TestMethod]
    public async Task ShutDownAsync_WaitCancelled_EndsQuietly()
    {
        var connection = new ScriptedConnection(Read("221 Bye\r\n")) { WritesBeforeFailure = 0, WriteFailure = new OperationCanceledException() };
        var kept = new SmtpKeptConnection(connection, string.Empty, TimeProvider.System) { QuitsOnShutDown = true };

        await kept.ShutDownAsync(CancellationToken.None);

        Assert.AreEqual(0, connection.ReadCount);
    }

    /// <summary>One upload's commands after <c>EHLO</c>, to a server that advertises no <c>SIZE</c>.</summary>
    internal const string Envelope = "EHLO mail.txt\r\nMAIL FROM:<a@x>\r\nRCPT TO:<b@x>\r\nDATA\r\nx\r\n.\r\n";

    internal static TransferContext Context(string? user = null) => new()
    {
        Credentials = user is null ? null : new NetworkCredential(user, "p"),
        Url = CurlUrl.Parse("smtp://127.0.0.1:18118/mail.txt"),
        Output = Stream.Null,
        Upload = new MemoryStream(Encoding.Latin1.GetBytes("x\r\n")),
        Mail = new MailRequestOptions { From = "a@x", Recipients = ["b@x"] },
    };

    internal static byte[] Read(string text) => Encoding.Latin1.GetBytes(text);

    /// <summary>
    /// A scripted connection the run's connection cache would pool: it holds a session, and
    /// disposing it after <see cref="MarkReusable" /> keeps it open, as the cache does.
    /// </summary>
    internal sealed class PoolingConnection(params byte[][] reads) : IConnection
    {
        private readonly ScriptedConnection inner = new(reads);

        private bool markedReusable;

        private IConnectionSession? session;

        public string SentText => Encoding.Latin1.GetString(inner.Sent);

        public bool IsClosed { get; private set; }

        public bool IsSecure => false;

        public EndPoint? RemoteEndPoint => null;

        public IConnectionSession? Session => session;

        public bool TryHoldSession(IConnectionSession held)
        {
            session = held;
            return true;
        }

        public void MarkReusable() => markedReusable = true;

        public ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken) => inner.ReadAsync(buffer, cancellationToken);

        public ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken) => inner.WriteAsync(buffer, cancellationToken);

        public ValueTask FlushAsync(CancellationToken cancellationToken) => ValueTask.CompletedTask;

        public async ValueTask CloseAsync()
        {
            if (session is not null)
            {
                await session.ShutDownAsync(CancellationToken.None);
            }

            session = null;
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
