using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Curl.Networking.Fakes;
using Curl.Protocol.Abstractions;

namespace Curl.Networking;

/// <summary>
/// STD-192: with a server that requires a client certificate none is sent for, the Schannel build
/// prints <c>failed to read data from server: SEC_E_ILLEGAL_MESSAGE</c>, even where
/// <c>SslStream</c> reports the read as the socket abort that follows the server's alert.
/// </summary>
[TestClass]
public sealed class Std192ClientCertificateRefusalTests
{
    private static IOException Abort(SocketError error) =>
        new("read", new SocketException((int)error));

    [TestMethod]
    [DataRow(SocketError.ConnectionAborted)]
    [DataRow(SocketError.ConnectionReset)]
    public void AlertReadFailure_SchannelCertificateRequestedNonePresentedSocketEnded_ReportsIllegalMessage(SocketError error)
    {
        var message = TlsFailureMessages.AlertReadFailure(
            Abort(error), matchesSchannelBuild: true, presentedClientCertificate: false, serverRequestedClientCertificate: true);

        Assert.IsNotNull(message);
        StringAssert.Contains(message, "schannel: failed to read data from server: SEC_E_ILLEGAL_MESSAGE (0x80090326)", StringComparison.Ordinal);
    }

    [TestMethod]
    public void AlertReadFailure_SchannelNoCertificateRequested_IsNull() =>
        Assert.IsNull(TlsFailureMessages.AlertReadFailure(
            Abort(SocketError.ConnectionReset), matchesSchannelBuild: true, presentedClientCertificate: false));

    [TestMethod]
    public void AlertReadFailure_SchannelCertificateRequestedFailureHasNoSocketError_IsNull() =>
        Assert.IsNull(TlsFailureMessages.AlertReadFailure(
            new IOException("read"), matchesSchannelBuild: true, presentedClientCertificate: false, serverRequestedClientCertificate: true));

    [TestMethod]
    public void AlertReadFailure_SchannelCertificateWasPresented_IsNull() =>
        Assert.IsNull(TlsFailureMessages.AlertReadFailure(
            Abort(SocketError.ConnectionReset), matchesSchannelBuild: true, presentedClientCertificate: true, serverRequestedClientCertificate: true));

    [TestMethod]
    public void AlertReadFailure_SchannelCertificateRequestedOtherSocketError_IsNull() =>
        Assert.IsNull(TlsFailureMessages.AlertReadFailure(
            Abort(SocketError.TimedOut), matchesSchannelBuild: true, presentedClientCertificate: false, serverRequestedClientCertificate: true));

    [TestMethod]
    public async Task ReadAsync_SchannelServerAskedForCertificateAndFirstReadIsReset_ReportsIllegalMessage()
    {
        await using var scenario = await ConnectAsync(serverRequested: true);
        scenario.Stream.FailReads = true;

        var failure = await Assert.ThrowsAsync<MissingCloseNotifyException>(
            () => scenario.Connection.ReadAsync(new byte[16], CancellationToken.None).AsTask());

        StringAssert.Contains(failure.Message, "SEC_E_ILLEGAL_MESSAGE", StringComparison.Ordinal);
    }

    [TestMethod]
    public async Task ReadAsync_SchannelNoCertificateAsked_LeavesTheResetAsItIs()
    {
        await using var scenario = await ConnectAsync(serverRequested: false);
        scenario.Stream.FailReads = true;

        var failure = await Assert.ThrowsAsync<IOException>(
            () => scenario.Connection.ReadAsync(new byte[16], CancellationToken.None).AsTask());

        Assert.IsNotInstanceOfType<MissingCloseNotifyException>(failure);
    }

    [TestMethod]
    public async Task ReadAsync_SchannelResetAfterPlaintextWasRead_LeavesTheResetAsItIs()
    {
        await using var scenario = await ConnectAsync(serverRequested: true);
        await scenario.ServerStream.WriteAsync(new byte[] { 1 });
        Assert.AreEqual(1, await scenario.Connection.ReadAsync(new byte[16], CancellationToken.None));
        scenario.Stream.FailReads = true;

        var failure = await Assert.ThrowsAsync<IOException>(
            () => scenario.Connection.ReadAsync(new byte[16], CancellationToken.None).AsTask());

        Assert.IsNotInstanceOfType<MissingCloseNotifyException>(failure);
    }

    private static async Task<Scenario> ConnectAsync(bool serverRequested)
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest("CN=localhost", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var ephemeral = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
        var certificate = X509CertificateLoader.LoadPkcs12(ephemeral.Export(X509ContentType.Pkcs12), null);
        var (client, server) = InMemoryDuplexStream.CreatePair();
        var stream = new ResettableStream(client);
        var sslStream = new SslStream(stream, false, (_, _, _, _) => true);
        var serverStream = new SslStream(server);
        var serverTask = serverStream.AuthenticateAsServerAsync(new SslServerAuthenticationOptions { ServerCertificate = certificate });
        await sslStream.AuthenticateAsClientAsync(new SslClientAuthenticationOptions { TargetHost = "localhost" });
        await serverTask;
        var plaintext = new StallingConnection();
        var connection = new SslStreamConnection(sslStream, new ConnectionStream(plaintext), plaintext, null, string.Empty, matchesOpenSslBuild: false)
        {
            MatchesSchannelBuild = true,
        };
        if (serverRequested)
        {
            connection = new SslStreamConnection(sslStream, new ConnectionStream(plaintext), plaintext, null, string.Empty, matchesOpenSslBuild: false)
            {
                MatchesSchannelBuild = true,
                ServerRequestedClientCertificate = static () => true,
            };
        }

        return new Scenario(connection, stream, serverStream, certificate);
    }

    private sealed class Scenario(SslStreamConnection connection, ResettableStream stream, SslStream serverStream, X509Certificate2 certificate) : IAsyncDisposable
    {
        public SslStreamConnection Connection { get; } = connection;

        public ResettableStream Stream { get; } = stream;

        public SslStream ServerStream { get; } = serverStream;

        public async ValueTask DisposeAsync()
        {
            await ServerStream.DisposeAsync();
            certificate.Dispose();
        }
    }

    // Passes everything through until FailReads, then a read fails as a socket reset does.
    private sealed class ResettableStream(Stream inner) : Stream
    {
        public bool FailReads { get; set; }

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
            FailReads
                ? throw new IOException("read", new SocketException((int)SocketError.ConnectionReset))
                : inner.ReadAsync(buffer, cancellationToken);

        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default) =>
            inner.WriteAsync(buffer, cancellationToken);

        public override Task FlushAsync(CancellationToken cancellationToken) => inner.FlushAsync(cancellationToken);

        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override void Flush()
        {
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();
    }
}
