using System.Net;
using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Original.Protocol.Ssh.Fakes;

namespace Curl.Original.Protocol.Ssh;

/// <summary>
/// Pins CGR-130 (BL-3659): several <c>sftp://</c> or <c>scp://</c> URLs of one login reuse
/// one SSH connection through the run's connection cache, as curl 8.22.0 does - one
/// handshake, one authentication and, over SFTP, one subsystem and one <c>REALPATH</c>, with
/// the first transfer counting one new connection and the second none - and the cache's
/// close ends the session with the SFTP channel's close and <c>DISCONNECT</c> 11
/// <c>Shutdown</c>.
/// </summary>
[TestClass]
public sealed class Cgr130Tests
{
    private const string User = "tester";

    private const string Password = "secret";

    [TestMethod]
    public async Task ExecuteAsync_TwoSftpUrlsThroughTheConnectionCache_ShareOneConnectionAndOneSftpSession()
    {
        InMemorySshServer server = NewServer();
        KeepingConnector cache = new(server);

        (TransferResult first, byte[] hello) = await RunAsync(cache, "sftp://files.example/data/hello.txt", Password);
        (TransferResult second, byte[] digits) = await RunAsync(cache, "sftp://files.example/data/digits.txt", Password);
        await cache.CloseAsync();
        await server.WhenSessionsEndAsync();

        Assert.AreEqual("hello world", Encoding.ASCII.GetString(hello));
        Assert.AreEqual("0123456789", Encoding.ASCII.GetString(digits));
        Assert.AreEqual(1, first.Report?.ConnectionCount);
        Assert.AreEqual(0, second.Report?.ConnectionCount);
        Assert.AreEqual(11, first.Report?.DownloadSize);
        Assert.AreEqual(10, second.Report?.DownloadSize);
        Assert.HasCount(1, server.Targets);
        Assert.AreEqual(1, server.Events.Count(line => line.StartsWith("auth password", StringComparison.Ordinal)));
        Assert.AreEqual(1, server.Events.Count(line => line == "subsystem sftp"));
        Assert.AreEqual(1, server.Events.Count(line => line == "sftp 16 ."));
        CollectionAssert.AreEqual(
            new[] { "channel eof", "channel close", "disconnect 11 Shutdown" },
            server.Events.SkipWhile(line => line != "sftp 4 /data/digits.txt").Skip(1).Where(line => line != "closed").ToArray());
    }

    [TestMethod]
    public async Task ExecuteAsync_AnotherPasswordAfterAKeptSession_ClosesItAndConnectsAgain()
    {
        InMemorySshServer server = NewServer();
        KeepingConnector cache = new(server);

        await RunAsync(cache, "sftp://files.example/data/hello.txt", Password);
        (TransferResult second, _) = await RunAsync(cache, "sftp://files.example/data/digits.txt", "other");
        await cache.CloseAsync();
        await server.WhenSessionsEndAsync();

        Assert.AreEqual(CurlExitCode.LoginDenied, second.ExitCode);
        Assert.HasCount(2, server.Targets);
        Assert.AreEqual(2, server.Events.Count(line => line == "disconnect 11 Shutdown"));
    }

    [TestMethod]
    public async Task ExecuteAsync_AFailedSftpTransfer_IsNotKeptForTheNextUrl()
    {
        InMemorySshServer server = NewServer();
        KeepingConnector cache = new(server);

        (TransferResult first, _) = await RunAsync(cache, "sftp://files.example/data/missing.txt", Password);
        (TransferResult second, byte[] digits) = await RunAsync(cache, "sftp://files.example/data/digits.txt", Password);
        await cache.CloseAsync();
        await server.WhenSessionsEndAsync();

        Assert.IsFalse(first.IsSuccess);
        Assert.AreEqual("0123456789", Encoding.ASCII.GetString(digits));
        Assert.AreEqual(1, second.Report?.ConnectionCount);
        Assert.HasCount(2, server.Targets);
    }

    [TestMethod]
    public async Task ExecuteAsync_AFailedTransferOnAReusedConnection_SendsOneDisconnect()
    {
        InMemorySshServer server = NewServer();
        KeepingConnector cache = new(server);

        await RunAsync(cache, "sftp://files.example/data/hello.txt", Password);
        (TransferResult second, _) = await RunAsync(cache, "sftp://files.example/data/missing.txt", Password);
        await cache.CloseAsync();
        await server.WhenSessionsEndAsync();

        Assert.IsFalse(second.IsSuccess);
        Assert.HasCount(1, server.Targets);
        Assert.AreEqual(1, server.Events.Count(line => line.StartsWith("disconnect", StringComparison.Ordinal)));
        Assert.AreEqual(0, cache.WritesWhileClosing);
    }

    [TestMethod]
    public async Task ExecuteAsync_AReusedConnectionWithNoSshSession_IsClosedAndAnotherOpened()
    {
        InMemorySshServer server = NewServer();
        KeepingConnector cache = new(server) { HandsOutAnEmptyReusedConnectionFirst = true };

        (TransferResult result, byte[] hello) = await RunAsync(cache, "sftp://files.example/data/hello.txt", Password);
        await cache.CloseAsync();
        await server.WhenSessionsEndAsync();

        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual("hello world", Encoding.ASCII.GetString(hello));
        Assert.HasCount(2, server.Targets);
    }

    [TestMethod]
    public async Task SshSessionConnection_SwitchedToAnotherConnection_ReportsItAndLeavesItUndisposed()
    {
        PlainConnection first = new(isSecure: false, new IPEndPoint(IPAddress.Loopback, 22));
        PlainConnection second = new(isSecure: true, new IPEndPoint(IPAddress.Loopback, 2222));
        SshSessionConnection connection = new(first);

        connection.Current = second;
        await connection.DisposeAsync();

        Assert.IsTrue(connection.IsSecure);
        Assert.AreEqual(second.RemoteEndPoint, connection.RemoteEndPoint);
        Assert.IsFalse(second.IsDisposed);
    }

    private static InMemorySshServer NewServer()
    {
        InMemorySshServer server = new(User, Password);
        server.Files["/data/hello.txt"] = "hello world"u8.ToArray();
        server.Files["/data/digits.txt"] = "0123456789"u8.ToArray();
        return server;
    }

    private static async Task<(TransferResult Result, byte[] Output)> RunAsync(IConnector connector, string url, string password)
    {
        SshProtocolHandler handler = new(
            connector,
            new InMemoryKeyFileSystem(new Dictionary<string, string>()),
            SshAlgorithmPreferences.OpenSslReference,
            Encoding.UTF8,
            new SystemSshRandomSource(),
            new SystemSshEphemeralKeySource(),
            Environment.GetEnvironmentVariable,
            new UnreachableSshAgent());
        MemoryStream output = new();
        TransferResult result = await handler.ExecuteAsync(new TransferContext
        {
            Url = CurlUrl.Parse(url),
            Output = output,
            Credentials = new NetworkCredential(User, password),
        });
        return (result, output.ToArray());
    }

    private sealed class PlainConnection(bool isSecure, EndPoint remoteEndPoint) : IConnection
    {
        public bool IsDisposed { get; private set; }

        public bool IsSecure => isSecure;

        public EndPoint? RemoteEndPoint => remoteEndPoint;

        public ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken) => ValueTask.FromResult(0);

        public ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken) => ValueTask.CompletedTask;

        public ValueTask FlushAsync(CancellationToken cancellationToken) => ValueTask.CompletedTask;

        public ValueTask DisposeAsync()
        {
            IsDisposed = true;
            return ValueTask.CompletedTask;
        }
    }

    // The run's connection cache in miniature: a connection marked reusable is kept, with the
    // session it holds, and handed to the next connect as reused; any other is closed, its
    // session shut down first.
    private sealed class KeepingConnector(IConnector inner) : IConnector
    {
        private KeptConnection? idle;

        // What the client wrote while a connection's session was shut down at its close. The
        // in-memory server stops reading at the first DISCONNECT, so a second one shows only
        // here (BL-4059).
        public int WritesWhileClosing { get; set; }

        public bool HandsOutAnEmptyReusedConnectionFirst { get; set; }

        public async ValueTask<ConnectResult> ConnectAsync(ConnectTarget target, CancellationToken cancellationToken)
        {
            if (idle is null && HandsOutAnEmptyReusedConnectionFirst)
            {
                HandsOutAnEmptyReusedConnectionFirst = false;
                idle = await OpenAsync(target, cancellationToken);
            }

            if (idle is { } kept)
            {
                idle = null;
                kept.IsReusable = false;
                return ConnectResult.Connected(kept, null, isReused: true);
            }

            return ConnectResult.Connected(await OpenAsync(target, cancellationToken));
        }

        public async Task CloseAsync()
        {
            if (idle is { } kept)
            {
                idle = null;
                await kept.CloseAsync();
            }
        }

        internal void Keep(KeptConnection connection) => idle = connection;

        private async Task<KeptConnection> OpenAsync(ConnectTarget target, CancellationToken cancellationToken) =>
            new(this, (await inner.ConnectAsync(target, cancellationToken)).Connection!);
    }

    private sealed class KeptConnection(KeepingConnector cache, IConnection inner) : IConnection
    {
        public bool IsReusable { get; set; }

        public bool IsSecure => false;

        public EndPoint? RemoteEndPoint => null;

        public IConnectionSession? Session { get; private set; }

        public ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken) => inner.ReadAsync(buffer, cancellationToken);

        private bool isClosing;

        public ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken)
        {
            if (isClosing)
            {
                cache.WritesWhileClosing++;
            }

            return inner.WriteAsync(buffer, cancellationToken);
        }

        public ValueTask FlushAsync(CancellationToken cancellationToken) => inner.FlushAsync(cancellationToken);

        public void MarkReusable() => IsReusable = true;

        public bool TryHoldSession(IConnectionSession session)
        {
            Session = session;
            return true;
        }

        public async ValueTask DisposeAsync()
        {
            if (IsReusable)
            {
                cache.Keep(this);
                return;
            }

            await CloseAsync();
        }

        public async Task CloseAsync()
        {
            if (Session is { } session)
            {
                isClosing = true;
                await session.ShutDownAsync(CancellationToken.None);
                isClosing = false;
            }

            await inner.DisposeAsync();
        }
    }
}
