using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

using Curl.Networking.Fakes;

namespace Curl.Networking;

/// <summary>
/// Disposing a TLS connection sends <c>close_notify</c> under a bound and always closes the
/// transport (BL-4064), whatever the send or the stream's own disposal throws.
/// </summary>
[TestClass]
public sealed class CloseNotifyOnDisposeTests
{
    private static readonly TimeSpan s_bound = TimeSpan.FromMilliseconds(100);

    private static readonly TimeSpan s_generousWait = TimeSpan.FromSeconds(30);

    [TestMethod]
    public async Task DisposeAsync_OfAnSslStreamConnectionWhoseWritesNeverComplete_ReturnsWithinTheBoundAndDisposesTheTransport()
    {
        using var certificate = CreateServerCertificate();
        var (client, server) = InMemoryDuplexStream.CreatePair();
        var gated = new WriteGatedStream(client);
        var sslStream = new SslStream(gated, false, (_, _, _, _) => true);
        await using var serverStream = new SslStream(server);
        var serverTask = serverStream.AuthenticateAsServerAsync(new SslServerAuthenticationOptions { ServerCertificate = certificate });
        await sslStream.AuthenticateAsClientAsync(new SslClientAuthenticationOptions { TargetHost = "localhost" });
        await serverTask;
        var plaintext = new StallingConnection();
        var connection = new SslStreamConnection(sslStream, new ConnectionStream(plaintext), plaintext, null, string.Empty, matchesOpenSslBuild: false)
        {
            CloseNotifyTimeout = s_bound,
        };
        gated.StallWrites = true;

        await connection.DisposeAsync().AsTask().WaitAsync(s_generousWait);

        Assert.IsTrue(plaintext.IsDisposed);
    }

    // SslStream itself refuses a second shutdown, so the wire cannot tell a second attempt;
    // each attempt starts its close_notify bound on the connection's clock, which can (AF-0606).
    [TestMethod]
    public async Task DisposeAsync_OfAnSslStreamConnectionCalledTwice_TriesToSendCloseNotifyOnlyOnce()
    {
        using var certificate = CreateServerCertificate();
        var (client, server) = InMemoryDuplexStream.CreatePair();
        var sslStream = new SslStream(client, false, (_, _, _, _) => true);
        await using var serverStream = new SslStream(server);
        var serverTask = serverStream.AuthenticateAsServerAsync(new SslServerAuthenticationOptions { ServerCertificate = certificate });
        await sslStream.AuthenticateAsClientAsync(new SslClientAuthenticationOptions { TargetHost = "localhost" });
        await serverTask;
        var plaintext = new StallingConnection();
        var clock = new TimerCountingTimeProvider();
        var connection = new SslStreamConnection(sslStream, new ConnectionStream(plaintext), plaintext, null, string.Empty, matchesOpenSslBuild: false)
        {
            CloseNotifyTimeout = s_bound,
            TimeProvider = clock,
        };

        await connection.DisposeAsync().AsTask().WaitAsync(s_generousWait);
        await connection.DisposeAsync().AsTask().WaitAsync(s_generousWait);

        Assert.AreEqual(1, clock.TimersCreated, "Disposing a second time tried to send close_notify again.");
    }

    // The hand-built streams refuse a second shutdown too, so here as well only the clock can
    // tell a second close_notify attempt (AF-0639).
    [TestMethod]
    public async Task DisposeAsync_OfAHandBuiltTlsConnectionCalledTwice_TriesToSendCloseNotifyOnlyOnce()
    {
        var plaintext = new StallingConnection();
        var clock = new TimerCountingTimeProvider();
        var connection = new HandBuiltTlsConnection(new MemoryStream(), plaintext, null, string.Empty, matchesOpenSslBuild: false)
        {
            CloseNotifyTimeout = s_bound,
            TimeProvider = clock,
        };

        await connection.DisposeAsync().AsTask().WaitAsync(s_generousWait);
        await connection.DisposeAsync().AsTask().WaitAsync(s_generousWait);

        Assert.AreEqual(1, clock.TimersCreated, "Disposing a second time tried to send close_notify again.");
    }

    [TestMethod]
    public async Task DisposeAsync_OfAnSslStreamConnectionWhoseStreamDisposalThrows_StillDisposesTheTransport()
    {
        var plaintext = new StallingConnection();
        var sslStream = new SslStream(new ThrowingOnDisposeStream());
        var connection = new SslStreamConnection(sslStream, new ConnectionStream(plaintext), plaintext, null, string.Empty, matchesOpenSslBuild: false);

        await connection.DisposeAsync();

        Assert.IsTrue(plaintext.IsDisposed);
    }

    [TestMethod]
    public async Task DisposeAsync_OfAHandBuiltTlsConnectionWhoseStreamDisposalThrows_StillDisposesTheTransport()
    {
        var plaintext = new StallingConnection();
        var connection = new HandBuiltTlsConnection(new ThrowingOnDisposeStream(), plaintext, null, string.Empty, matchesOpenSslBuild: false);

        await connection.DisposeAsync();

        Assert.IsTrue(plaintext.IsDisposed);
    }

    private static X509Certificate2 CreateServerCertificate()
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest("CN=localhost", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var ephemeral = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
        return X509CertificateLoader.LoadPkcs12(ephemeral.Export(X509ContentType.Pkcs12), null);
    }

    // Passes everything through until StallWrites, then a write waits for its cancellation as a full send buffer would.
    private sealed class WriteGatedStream(Stream inner) : Stream
    {
        public bool StallWrites { get; set; }

        public override bool CanRead => true;

        public override bool CanWrite => true;

        public override bool CanSeek => false;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
            inner.ReadAsync(buffer, cancellationToken);

        public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (StallWrites)
            {
                await Task.Delay(Timeout.Infinite, cancellationToken);
            }

            await inner.WriteAsync(buffer, cancellationToken);
        }

        public override Task FlushAsync(CancellationToken cancellationToken) => inner.FlushAsync(cancellationToken);

        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override void Flush()
        {
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();
    }

    // The system clock, counting the timers created on it.
    private sealed class TimerCountingTimeProvider : TimeProvider
    {
        private int _timersCreated;

        public int TimersCreated => Volatile.Read(ref _timersCreated);

        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            Interlocked.Increment(ref _timersCreated);
            return System.CreateTimer(callback, state, dueTime, period);
        }
    }

    private sealed class ThrowingOnDisposeStream : MemoryStream
    {
        public override ValueTask DisposeAsync() => throw new InvalidOperationException("disposal failed");
    }
}
