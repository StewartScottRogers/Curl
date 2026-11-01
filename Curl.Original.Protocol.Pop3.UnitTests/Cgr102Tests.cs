using System.Net;
using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Original.Protocol.Pop3.Fakes;

namespace Curl.Original.Protocol.Pop3;

/// <summary>
/// Pins CGR-102 (BL-3631): curl keeps one POP3 connection for every URL of the same login, where
/// Curl kept it for only two, because the kept session marked the first transfer's lease reusable
/// rather than the lease each later transfer was handed. Measured 2026-10-26 with
/// <c>Record-CurlExchange.ps1 -Pop3</c>, curl 8.22.0 (Schannel) running
/// <c>-sS -u u:p pop3://127.0.0.1:18631/1 pop3://127.0.0.1:18631/2 pop3://127.0.0.1:18631/3</c>:
/// one connection carrying <c>CAPA</c>, <c>AUTH PLAIN</c>, <c>AHUAcA==</c>, <c>RETR 1</c> to
/// <c>RETR 3</c> and one <c>QUIT</c>.
/// </summary>
[TestClass]
public sealed class Cgr102Tests
{
    private const string Message = "+OK 3 octets\r\nhi\r\n.\r\n";

    [TestMethod]
    public async Task ExecuteAsync_ThreeUrlsEachOnTheirOwnLease_KeepOneConnectionAndQuitOnceAsCurlDoes()
    {
        var line = new PooledLine(
            Read("+OK POP3 ready <1896.697170952@localhost>\r\n"), Read("+OK Capability list follows\r\nSASL PLAIN\r\n.\r\n"), Read("+ \r\n+OK Authenticated\r\n"),
            Read(Message), Read(Message), Read(Message), Read("+OK Bye\r\n"));
        var connector = new QueuedConnector(
            ConnectResult.Connected(line.Lease(), null), ConnectResult.Connected(line.Lease(), null, isReused: true), ConnectResult.Connected(line.Lease(), null, isReused: true));
        var handler = new Pop3ProtocolHandler(connector, new QueuedTlsProvider(), new ScriptedSaslAuthenticator(("PLAIN", ["\0u\0p"])));

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
        Assert.AreEqual("CAPA\r\nAUTH PLAIN\r\nAHUAcA==\r\nRETR 1\r\nRETR 2\r\nRETR 3\r\nQUIT\r\n", line.SentText);
    }

    [TestMethod]
    public async Task ExecuteAsync_ReusedLeaseHoldingNoPop3Session_LogsInAgain()
    {
        var line = new PooledLine(
            Read("+OK POP3 ready <1896.697170952@localhost>\r\n"), Read("+OK Capability list follows\r\nSASL PLAIN\r\n.\r\n"), Read("+ \r\n+OK Authenticated\r\n"), Read(Message));
        var connector = new QueuedConnector(ConnectResult.Connected(line.Lease(), null, isReused: true));
        var handler = new Pop3ProtocolHandler(connector, new QueuedTlsProvider(), new ScriptedSaslAuthenticator(("PLAIN", ["\0u\0p"])));

        TransferResult result = await handler.ExecuteAsync(Context("1"));

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual("CAPA\r\nAUTH PLAIN\r\nAHUAcA==\r\nRETR 1\r\n", line.SentText);
    }

    private static TransferContext Context(string message) => new()
    {
        Url = CurlUrl.Parse("pop3://127.0.0.1:18631/" + message),
        Output = Stream.Null,
        Credentials = new NetworkCredential("u", "p"),
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
