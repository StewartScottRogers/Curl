using System.Net;
using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Original.Protocol.Smtp.Fakes;

namespace Curl.Original.Protocol.Smtp;

/// <summary>
/// Pins CGR-102 (BL-3631): curl keeps one SMTP connection for every URL of the same login, where
/// Curl kept it for only two, because the kept session would keep only the first transfer's lease,
/// not the lease each later transfer was handed. Measured 2026-10-26 with
/// <c>Record-CurlExchange.ps1 -Smtp</c>, curl 8.22.0 (Schannel) running
/// <c>-sS --mail-rcpt x smtp://127.0.0.1:18633/1 smtp://127.0.0.1:18633/2 smtp://127.0.0.1:18633/3</c>:
/// one connection carrying <c>EHLO 1</c>, <c>VRFY x</c> three times and one <c>QUIT</c>.
/// </summary>
[TestClass]
public sealed class Cgr102Tests
{
    private const string Verified = "250 x@localhost\r\n";

    [TestMethod]
    public async Task ExecuteAsync_ThreeVerifiesEachOnTheirOwnLease_KeepOneConnectionAndQuitOnceAsCurlDoes()
    {
        var line = new PooledLine(
            Read("220 localhost ESMTP\r\n"), Read("250 localhost\r\n"), Read(Verified), Read(Verified), Read(Verified), Read("221 Bye\r\n"));
        var connector = new QueuedConnector(
            ConnectResult.Connected(line.Lease(), null), ConnectResult.Connected(line.Lease(), null, isReused: true), ConnectResult.Connected(line.Lease(), null, isReused: true));
        var handler = new SmtpProtocolHandler(connector, new QueuedTlsProvider());

        CurlExitCode[] exitCodes =
        [
            (await handler.ExecuteAsync(Context("1"))).ExitCode,
            (await handler.ExecuteAsync(Context("2"))).ExitCode,
            (await handler.ExecuteAsync(Context("3"))).ExitCode,
        ];
        bool closedBeforeExit = line.IsClosed;
        await line.CloseAsync();

        CollectionAssert.AreEqual(new[] { CurlExitCode.Ok, CurlExitCode.Ok, CurlExitCode.Ok }, exitCodes);
        Assert.IsFalse(closedBeforeExit);
        Assert.AreEqual("EHLO 1\r\nVRFY x\r\nVRFY x\r\nVRFY x\r\nQUIT\r\n", line.SentText);
    }

    private static TransferContext Context(string path) => new()
    {
        Url = CurlUrl.Parse("smtp://127.0.0.1:18633/" + path),
        Output = Stream.Null,
        Mail = new MailRequestOptions { Recipients = ["x"] },
    };

    private static byte[] Read(string text) => Encoding.Latin1.GetBytes(text);

    /// <summary>
    /// One scripted connection the run's connection cache pools, handed to each transfer as a
    /// lease of its own, as the cache's <c>PooledConnection</c> is: the session belongs to the
    /// connection, and disposing a lease the transfer did not mark reusable closes it.
    /// </summary>
    private sealed class PooledLine(params byte[][] reads)
    {
        private readonly ScriptedConnection inner = new(reads);

        private IConnectionSession? session;

        public string SentText => Encoding.Latin1.GetString(inner.Sent);

        public bool IsClosed { get; private set; }

        public IConnection Lease() => new LineLease(this);

        public async ValueTask CloseAsync()
        {
            if (IsClosed)
            {
                return;
            }

            IsClosed = true;
            if (session is not null)
            {
                await session.ShutDownAsync(CancellationToken.None);
            }
        }

        private sealed class LineLease(PooledLine line) : IConnection
        {
            private bool markedReusable;

            public IConnectionSession? Session => line.session;

            public bool IsSecure => false;

            public EndPoint? RemoteEndPoint => null;

            public bool TryHoldSession(IConnectionSession session)
            {
                line.session = session;
                return true;
            }

            public void MarkReusable() => markedReusable = true;

            public ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken) => line.inner.ReadAsync(buffer, cancellationToken);

            public ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken) => line.inner.WriteAsync(buffer, cancellationToken);

            public ValueTask FlushAsync(CancellationToken cancellationToken) => ValueTask.CompletedTask;

            public async ValueTask DisposeAsync()
            {
                if (!markedReusable)
                {
                    await line.CloseAsync();
                }
            }
        }
    }
}
