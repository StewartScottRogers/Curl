using System.Net;
using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Smtp.Fakes;

namespace Curl.Protocol.Smtp;

/// <summary>
/// Pins the command log of two uploads to one SMTP server against curl 8.22.0 (BL-3167), measured
/// 2026-10-22 with <c>Record-CurlExchange.ps1 -Smtp</c>, curl (Schannel) running
/// <c>-sS --mail-from a@x --mail-rcpt b@x -T mail.txt smtp://127.0.0.1:18118/ -T mail.txt smtp://127.0.0.1:18118/</c>:
/// one connection carrying <c>EHLO</c>, both envelopes and one <c>QUIT</c>. Curl opens and quits a
/// connection per URL today; BL-3172 makes it reuse one and removes the <c>[Ignore]</c>.
/// </summary>
[TestClass]
public sealed class SmtpProtocolHandlerTwoUrlTests
{
    /// <summary>curl 8.22.0's measured command log for the two uploads (see the class summary).</summary>
    private const string MeasuredTwoUrlLog =
        "EHLO mail.txt\r\nMAIL FROM:<a@x> SIZE=3\r\nRCPT TO:<b@x>\r\nDATA\r\nx\r\n.\r\n" +
        "MAIL FROM:<a@x> SIZE=3\r\nRCPT TO:<b@x>\r\nDATA\r\nx\r\n.\r\nQUIT\r\n";

    [TestMethod]
    [Ignore("BL-3172: the SMTP handler does not keep its connection between URLs yet.")]
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

    private static TransferContext Context() => new()
    {
        Url = CurlUrl.Parse("smtp://127.0.0.1:18118/mail.txt"),
        Output = Stream.Null,
        Upload = new MemoryStream(Encoding.Latin1.GetBytes("x\r\n")),
        Mail = new MailRequestOptions { From = "a@x", Recipients = ["b@x"] },
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

        private IConnectionSession? session;

        public string SentText => Encoding.Latin1.GetString(inner.Sent);

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
