using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Curl.Tls;
using CertificateRequest = System.Security.Cryptography.X509Certificates.CertificateRequest;

namespace Curl.Conformance;

/// <summary>
/// Pins the TLS 1.3 stand-in for a platform whose <see cref="SslStream"/> server cannot negotiate
/// TLS 1.3 (macOS, GF-0092): <see cref="TlsServerStream.AcceptAsync"/> with
/// <c>platformServesTls13: false</c> answers Curl's own TLS 1.3-only ClientHello with
/// <see cref="Tls13ServerStream"/>, so an <c>--ech</c> client meets a server that ignores ECH and
/// aborts with ech_required (exit 101, test4001) rather than on the protocol version (exit 35),
/// and hands any other first record back to <see cref="SslStream"/>.
/// </summary>
[TestClass]
public sealed class Tls13ServerStreamTests
{
    /// <summary>test4001's <c>--ech ecl:</c> config list, for <c>otherpublic.test.defo.ie</c>.</summary>
    private const string Test4001EchConfigList =
        "AEv+DQBHdAAgACCCU49qdxKOUXJPs3wlsM06v/t42sMH5xQOL37MAd3HaAAEAAEAAQAYb3RoZXJwdWJsaWMudGVzdC5kZWZvLmllAAA=";

    [TestMethod]
    [Timeout(60_000, CooperativeCancellation = true)]
    public async Task AcceptAsync_Tls13OnlyHelloWithoutPlatformTls13_HandBuiltServerCarriesBytesBothWays()
    {
        using X509Certificate2 certificate = CreateEcdsaCertificate();
        var options = new TlsServerOptions(certificate, ["h2", "http/1.1"], RequestClientCertificate: false);
        Tls13ClientSettings settings = new() { ServerName = "localhost", ApplicationProtocols = ["http/1.1"], SendLegacySessionId = true };

        (Stream server, Tls13ConnectResult client) = await ConnectAsync(options, settings);
        await using Stream serverStream = server;
        await using Tls13ClientStream clientStream = client.Stream!;
        Tls13ServerStream handBuilt = (Tls13ServerStream)serverStream;
        byte[] download = RandomNumberGenerator.GetBytes(40_000);
        await clientStream.WriteAsync("ping"u8.ToArray());
        byte[] ping = new byte[4];
        await serverStream.ReadExactlyAsync(ping);
        await serverStream.WriteAsync(download);
        byte[] downloaded = new byte[download.Length];
        await clientStream.ReadExactlyAsync(downloaded);
        await TlsServerStream.ShutdownAsync(serverStream);
        int afterCloseNotify = await clientStream.ReadAsync(new byte[1]);

        Assert.AreEqual("http/1.1", handBuilt.NegotiatedApplicationProtocol);
        CollectionAssert.AreEqual("ping"u8.ToArray(), ping);
        CollectionAssert.AreEqual(download, downloaded);
        Assert.AreEqual(0, afterCloseNotify);
        Assert.IsTrue(clientStream.CloseNotifyReceived);
    }

    [TestMethod]
    [Timeout(60_000, CooperativeCancellation = true)]
    public async Task AcceptAsync_Tls13OnlyHelloWithoutPlatformTls13_ClientCloseNotifyEndsServerRead()
    {
        using X509Certificate2 certificate = CreateEcdsaCertificate();
        var options = new TlsServerOptions(certificate, ["h2"], RequestClientCertificate: false);

        (Stream server, Tls13ConnectResult client) = await ConnectAsync(options, new Tls13ClientSettings { ServerName = "localhost" });
        await using Stream serverStream = server;
        await using Tls13ClientStream clientStream = client.Stream!;
        await clientStream.WriteAsync("a"u8.ToArray());
        await clientStream.ShutdownAsync();
        byte[] buffer = new byte[8];
        int first = await serverStream.ReadAsync(buffer.AsMemory(0, 8));
        int second = await serverStream.ReadAsync(buffer, 0, 8, CancellationToken.None);

        Assert.IsNull(((Tls13ServerStream)serverStream).NegotiatedApplicationProtocol);
        Assert.AreEqual(1, first);
        Assert.AreEqual(0, second);
    }

    [TestMethod]
    [Timeout(60_000, CooperativeCancellation = true)]
    public async Task ReadAsync_ClientGoneWithoutCloseNotify_ReadsZero()
    {
        using X509Certificate2 certificate = CreateEcdsaCertificate();
        var options = new TlsServerOptions(certificate, [], RequestClientCertificate: false);

        (Stream server, Tls13ConnectResult client) = await ConnectAsync(options, new Tls13ClientSettings { ServerName = "localhost" });
        await using Stream serverStream = server;
        await client.Stream!.DisposeAsync();
        int read = await serverStream.ReadAsync(new byte[8]);

        Assert.AreEqual(0, read);
    }

    [TestMethod]
    [Timeout(60_000, CooperativeCancellation = true)]
    public async Task AcceptAsync_EchOfferedToHandBuiltServer_ClientAbortsWithEchRequired()
    {
        using X509Certificate2 certificate = CreateEcdsaCertificate();
        var options = new TlsServerOptions(certificate, [], RequestClientCertificate: false);
        EchConfigList configs = EchConfigList.Decode(Convert.FromBase64String(Test4001EchConfigList)).Value;
        Tls13ClientSettings settings = new()
        {
            ServerName = "localhost",
            EncryptedClientHelloConfigs = configs,
            ExtensionOrder = [.. Tls13ClientSettings.DefaultExtensionOrder, TlsExtensionType.EncryptedClientHello],
        };
        (InMemoryDuplexStream serverEnd, InMemoryDuplexStream clientEnd) = InMemoryDuplexStream.CreatePair();

        Task<Stream> server = TlsServerStream.AcceptAsync(serverEnd, options, platformServesTls13: false, CancellationToken.None);
        Tls13ConnectResult client = await Tls13ClientConnection.ConnectAsync(clientEnd, settings, SystemTlsRandomSource.Instance, AcceptingVerifier.Instance, CancellationToken.None);
        await clientEnd.DisposeAsync();

        Assert.AreEqual(TlsAlertDescription.EchRequired, client.Failure!.Alert);
        await Assert.ThrowsAsync<Exception>(() => server);
    }

    [TestMethod]
    [Timeout(60_000, CooperativeCancellation = true)]
    public async Task AcceptAsync_HelloOfferingTls12WithoutPlatformTls13_IsReplayedToSslStream()
    {
        using X509Certificate2 certificate = CreateRsaCertificate();
        var options = new TlsServerOptions(certificate, [], RequestClientCertificate: false);
        (InMemoryDuplexStream serverEnd, InMemoryDuplexStream clientEnd) = InMemoryDuplexStream.CreatePair();
        var client = new SslStream(clientEnd, leaveInnerStreamOpen: false);
        var clientOptions = new SslClientAuthenticationOptions
        {
            TargetHost = "localhost",
            RemoteCertificateValidationCallback = static (_, _, _, _) => true,
        };

        Task<Stream> server = TlsServerStream.AcceptAsync(serverEnd, options, platformServesTls13: false, CancellationToken.None);
        await Task.WhenAll(server, client.AuthenticateAsClientAsync(clientOptions));
        await using Stream serverStream = await server;
        await using (client)
        {
            await client.WriteAsync("ping"u8.ToArray());
            byte[] ping = new byte[4];
            await serverStream.ReadExactlyAsync(ping);
            await TlsServerStream.ShutdownAsync(serverStream);

            Assert.IsInstanceOfType<SslStream>(serverStream);
            CollectionAssert.AreEqual("ping"u8.ToArray(), ping);
            Assert.AreEqual(0, await client.ReadAsync(new byte[1]));
        }
    }

    [TestMethod]
    [Timeout(60_000, CooperativeCancellation = true)]
    public async Task AcceptAsync_ClientCertificateRequested_Tls13OnlyHelloGoesToSslStream()
    {
        using X509Certificate2 certificate = CreateEcdsaCertificate();
        var options = new TlsServerOptions(certificate, [], RequestClientCertificate: true);
        (InMemoryDuplexStream serverEnd, InMemoryDuplexStream clientEnd) = InMemoryDuplexStream.CreatePair();

        Task<Stream> server = TlsServerStream.AcceptAsync(serverEnd, options, platformServesTls13: false, CancellationToken.None);
        _ = Tls13ClientConnection.ConnectAsync(clientEnd, new Tls13ClientSettings { ServerName = "localhost" }, SystemTlsRandomSource.Instance, AcceptingVerifier.Instance, CancellationToken.None);
        Exception? failure = null;
        try
        {
            await using Stream serverStream = await server;
            Assert.IsInstanceOfType<SslStream>(serverStream);
        }
        catch (Exception exception) when (!TlsServerStream.PlatformServesTls13)
        {
            // A platform whose SslStream serves no TLS 1.3 refuses the hello; either way it was SslStream's to answer.
            failure = exception;
        }

        await clientEnd.DisposeAsync();
        Assert.IsTrue(failure is null || !TlsServerStream.PlatformServesTls13);
    }

    [TestMethod]
    [Timeout(60_000, CooperativeCancellation = true)]
    public async Task AcceptAsync_PlainTextWithoutPlatformTls13_FailsAndDisposesInnerStream()
    {
        using X509Certificate2 certificate = CreateEcdsaCertificate();
        var options = new TlsServerOptions(certificate, [], RequestClientCertificate: false);
        (InMemoryDuplexStream serverEnd, InMemoryDuplexStream clientEnd) = InMemoryDuplexStream.CreatePair();

        Task<Stream> server = TlsServerStream.AcceptAsync(serverEnd, options, platformServesTls13: false, CancellationToken.None);
        await clientEnd.WriteAsync("GET / HTTP/1.1\r\n\r\n"u8.ToArray());
        await clientEnd.DisposeAsync();

        await Assert.ThrowsAsync<Exception>(() => server);
        await Assert.ThrowsExactlyAsync<ObjectDisposedException>(async () => await serverEnd.WriteAsync("x"u8.ToArray()));
    }

    [TestMethod]
    [Timeout(60_000, CooperativeCancellation = true)]
    public async Task AcceptAsync_ClientGoneBeforeFirstRecord_ThrowsAndDisposesInnerStream()
    {
        using X509Certificate2 certificate = CreateEcdsaCertificate();
        var options = new TlsServerOptions(certificate, [], RequestClientCertificate: false);
        (InMemoryDuplexStream serverEnd, InMemoryDuplexStream clientEnd) = InMemoryDuplexStream.CreatePair();
        await clientEnd.DisposeAsync();

        await Assert.ThrowsExactlyAsync<EndOfStreamException>(() => TlsServerStream.AcceptAsync(serverEnd, options, platformServesTls13: false, CancellationToken.None));
        await Assert.ThrowsExactlyAsync<ObjectDisposedException>(async () => await serverEnd.WriteAsync("x"u8.ToArray()));
    }

    [TestMethod]
    [Timeout(60_000, CooperativeCancellation = true)]
    public async Task AuthenticateAsync_ClientClosesBeforeFinished_ThrowsAuthenticationException()
    {
        using X509Certificate2 certificate = CreateEcdsaCertificate();
        var options = new TlsServerOptions(certificate, [], RequestClientCertificate: false);
        (InMemoryDuplexStream serverEnd, InMemoryDuplexStream clientEnd) = InMemoryDuplexStream.CreatePair();

        Task<Tls13ServerStream> server = Tls13ServerStream.AuthenticateAsync(serverEnd, ValidHello(), options, CancellationToken.None);
        byte[] serverHelloHeader = new byte[5];
        await clientEnd.ReadExactlyAsync(serverHelloHeader);
        await clientEnd.DisposeAsync();

        await Assert.ThrowsExactlyAsync<AuthenticationException>(() => server);
    }

    [TestMethod]
    [Timeout(60_000, CooperativeCancellation = true)]
    public async Task AuthenticateAsync_RecordThatDoesNotOpen_ThrowsIOException()
    {
        using X509Certificate2 certificate = CreateEcdsaCertificate();
        var options = new TlsServerOptions(certificate, [], RequestClientCertificate: false);
        (InMemoryDuplexStream serverEnd, InMemoryDuplexStream clientEnd) = InMemoryDuplexStream.CreatePair();

        Task<Tls13ServerStream> server = Tls13ServerStream.AuthenticateAsync(serverEnd, ValidHello(), options, CancellationToken.None);
        await clientEnd.WriteAsync(Record(20, [1]));
        await clientEnd.WriteAsync(Record(23, new byte[20]));

        await Assert.ThrowsExactlyAsync<IOException>(() => server);
        await clientEnd.DisposeAsync();
    }

    [TestMethod]
    public void Answer_NoX25519Share_IsRefused()
    {
        using X509Certificate2 certificate = CreateEcdsaCertificate();
        var options = new TlsServerOptions(certificate, [], RequestClientCertificate: false);

        Assert.ThrowsExactly<AuthenticationException>(() => Tls13ServerHandshake.Answer(Hello(Versions(0x0304)), options));
        Assert.ThrowsExactly<AuthenticationException>(() => Tls13ServerHandshake.Answer(Hello(Versions(0x0304), X25519Share(new byte[31])), options));
    }

    [TestMethod]
    public void Answer_NoSchemeTheKeyCanMake_IsRefused()
    {
        using X509Certificate2 certificate = CreateEcdsaCertificate();
        var options = new TlsServerOptions(certificate, [], RequestClientCertificate: false);
        TlsExtension share = X25519Share(ClientPublicKey());

        Assert.ThrowsExactly<AuthenticationException>(() => Tls13ServerHandshake.Answer(Hello(Versions(0x0304), share), options));
        Assert.ThrowsExactly<AuthenticationException>(() => Tls13ServerHandshake.Answer(Hello(Versions(0x0304), share, Schemes(TlsSignatureScheme.RsaPssRsaeSha256)), options));
    }

    [TestMethod]
    public void Answer_CertificateWithoutEcdsaKey_IsRefused()
    {
        using RSA key = RSA.Create(2048);
        using X509Certificate2 certificate = new CertificateRequest("CN=localhost", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1)
            .CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
        var options = new TlsServerOptions(certificate, [], RequestClientCertificate: false);

        Assert.ThrowsExactly<AuthenticationException>(() => Tls13ServerHandshake.Answer(ValidHello(), options));
    }

    [TestMethod]
    public void CheckClientFinished_AlertOrWrongMessage_IsRefused()
    {
        using X509Certificate2 certificate = CreateEcdsaCertificate();
        Tls13ServerHandshake handshake = Tls13ServerHandshake.Answer(ValidHello(), new TlsServerOptions(certificate, [], RequestClientCertificate: false));

        AuthenticationException alert = Assert.ThrowsExactly<AuthenticationException>(
            () => handshake.CheckClientFinished(new Tls13RecordContent(TlsContentType.Alert, [2, (byte)TlsAlertDescription.EchRequired])));
        AuthenticationException wrong = Assert.ThrowsExactly<AuthenticationException>(
            () => handshake.CheckClientFinished(new Tls13RecordContent(TlsContentType.Handshake, [20, 0, 0, 0])));
        Assert.ThrowsExactly<AuthenticationException>(
            () => handshake.CheckClientFinished(new Tls13RecordContent(TlsContentType.ApplicationData, handshake.ExpectedClientFinished)));
        handshake.CheckClientFinished(new Tls13RecordContent(TlsContentType.Handshake, handshake.ExpectedClientFinished));

        StringAssert.Contains(alert.Message, "EchRequired");
        StringAssert.Contains(wrong.Message, "Finished");
    }

    [TestMethod]
    public void FindTls13OnlyClientHello_ReadsOnlyAWholeTls13OnlyClientHello()
    {
        byte[] tls13Only = Hello(Versions(0x0304));

        CollectionAssert.AreEqual(tls13Only, TlsServerStream.FindTls13OnlyClientHello(Record(22, tls13Only)));
        Assert.IsNull(TlsServerStream.FindTls13OnlyClientHello(Record(23, tls13Only)));
        Assert.IsNull(TlsServerStream.FindTls13OnlyClientHello([22, 3, 3, 0, 0]));
        Assert.IsNull(TlsServerStream.FindTls13OnlyClientHello(Record(22, new HandshakeMessage(HandshakeType.ServerHello, [0]).Encode())));
        Assert.IsNull(TlsServerStream.FindTls13OnlyClientHello(Record(22, new HandshakeMessage(HandshakeType.ClientHello, [3]).Encode())));
        Assert.IsNull(TlsServerStream.FindTls13OnlyClientHello(Record(22, Hello())));
        Assert.IsNull(TlsServerStream.FindTls13OnlyClientHello(Record(22, Hello(new TlsExtension(TlsExtensionType.SupportedVersions, [9])))));
        Assert.IsNull(TlsServerStream.FindTls13OnlyClientHello(Record(22, Hello(Versions(0x0304, 0x0303)))));
    }

    [TestMethod]
    [Timeout(60_000, CooperativeCancellation = true)]
    public async Task Tls13ServerStream_IsAsynchronousOnlyAndCannotSeek()
    {
        using X509Certificate2 certificate = CreateEcdsaCertificate();
        (Stream server, Tls13ConnectResult client) = await ConnectAsync(new TlsServerOptions(certificate, [], false), new Tls13ClientSettings { ServerName = "localhost" });
        await using Stream stream = server;
        await using Tls13ClientStream clientStream = client.Stream!;
        await stream.WriteAsync([1, 2, 3], 0, 3, CancellationToken.None);
        await stream.FlushAsync();
        stream.Flush();
        byte[] received = new byte[3];
        await clientStream.ReadExactlyAsync(received);

        CollectionAssert.AreEqual(new byte[] { 1, 2, 3 }, received);
        AssertAsynchronousOnly(stream);
    }

    [TestMethod]
    [Timeout(60_000, CooperativeCancellation = true)]
    public async Task ReplayedPrefixStream_ReadsThePrefixThenTheInnerStream()
    {
        (InMemoryDuplexStream first, InMemoryDuplexStream second) = InMemoryDuplexStream.CreatePair();
        await using var stream = new ReplayedPrefixStream([1, 2, 3], first);
        await second.WriteAsync(new byte[] { 4 });
        await stream.WriteAsync(new byte[] { 9, 8 }, 0, 2, CancellationToken.None);
        await stream.WriteAsync(new byte[] { 7 });
        await stream.FlushAsync();
        stream.Flush();
        byte[] buffer = new byte[2];
        int firstRead = await stream.ReadAsync(buffer, 0, 2, CancellationToken.None);
        int secondRead = await stream.ReadAsync(buffer.AsMemory(firstRead));
        byte[] lastOfPrefix = new byte[1];
        await stream.ReadExactlyAsync(lastOfPrefix);
        byte[] fromInner = new byte[1];
        int innerRead = await stream.ReadAsync(fromInner);
        byte[] written = new byte[3];
        await second.ReadExactlyAsync(written);

        Assert.AreEqual(2, firstRead);
        Assert.AreEqual(0, secondRead);
        Assert.AreEqual(1, innerRead);
        CollectionAssert.AreEqual(new byte[] { 1, 2 }, buffer);
        Assert.AreEqual(3, lastOfPrefix[0]);
        Assert.AreEqual(4, fromInner[0]);
        CollectionAssert.AreEqual(new byte[] { 9, 8, 7 }, written);
        AssertAsynchronousOnly(stream);
    }

    private static void AssertAsynchronousOnly(Stream stream)
    {
        Assert.IsTrue(stream.CanRead);
        Assert.IsTrue(stream.CanWrite);
        Assert.IsFalse(stream.CanSeek);
        Assert.ThrowsExactly<NotSupportedException>(() => stream.Length);
        Assert.ThrowsExactly<NotSupportedException>(() => stream.Position);
        Assert.ThrowsExactly<NotSupportedException>(() => stream.Position = 0);
        Assert.ThrowsExactly<NotSupportedException>(() => stream.Seek(0, SeekOrigin.Begin));
        Assert.ThrowsExactly<NotSupportedException>(() => stream.SetLength(0));
        Assert.ThrowsExactly<NotSupportedException>(() => stream.Read(new byte[1], 0, 1));
        Assert.ThrowsExactly<NotSupportedException>(() => stream.Write(new byte[1], 0, 1));
    }

    private static async Task<(Stream Server, Tls13ConnectResult Client)> ConnectAsync(TlsServerOptions options, Tls13ClientSettings settings)
    {
        (InMemoryDuplexStream serverEnd, InMemoryDuplexStream clientEnd) = InMemoryDuplexStream.CreatePair();
        Task<Stream> server = TlsServerStream.AcceptAsync(serverEnd, options, platformServesTls13: false, CancellationToken.None);
        Tls13ConnectResult client = await Tls13ClientConnection.ConnectAsync(clientEnd, settings, SystemTlsRandomSource.Instance, AcceptingVerifier.Instance, CancellationToken.None);
        Assert.IsTrue(client.Succeeded, $"handshake failed: {client.Failure?.Alert}");
        return (await server, client);
    }

    private static byte[] ValidHello() =>
        Hello(Versions(0x0304), X25519Share(ClientPublicKey()), Schemes(TlsSignatureScheme.EcdsaSecp256r1Sha256));

    private static byte[] ClientPublicKey()
    {
        using X25519KeyShare share = new(RandomNumberGenerator.GetBytes(32));
        return share.PublicKey;
    }

    private static byte[] Hello(params TlsExtension[] extensions) =>
        new ClientHello(0x0303, new byte[32], [], [0x1301], [0], extensions).Encode();

    private static TlsExtension Versions(params ushort[] versions) => SupportedVersionsExtension.EncodeOffered(versions);

    private static TlsExtension X25519Share(byte[] publicKey) =>
        KeyShareExtension.EncodeClientShares([new KeyShareEntry(TlsNamedGroup.X25519, publicKey)]);

    private static TlsExtension Schemes(params ushort[] schemes) => SignatureAlgorithmsExtension.Encode(schemes);

    private static byte[] Record(byte type, byte[] content) =>
        [type, 3, 3, (byte)(content.Length >> 8), (byte)content.Length, .. content];

    /// <summary>A self-signed P-256 certificate, as upstream's generated test certificates are, reloaded through PKCS#12 so SslStream can serve it too.</summary>
    private static X509Certificate2 CreateEcdsaCertificate()
    {
        using ECDsa key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var request = new CertificateRequest("CN=localhost", key, HashAlgorithmName.SHA256);
        var alternativeNames = new SubjectAlternativeNameBuilder();
        alternativeNames.AddDnsName("localhost");
        request.CertificateExtensions.Add(alternativeNames.Build());
        using X509Certificate2 ephemeral = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
        return X509CertificateLoader.LoadPkcs12(ephemeral.Export(X509ContentType.Pkcs12), password: null);
    }

    /// <summary>A self-signed RSA certificate reloaded through PKCS#12, the kind every platform's SslStream server is known to serve (TlsServerStreamTests).</summary>
    private static X509Certificate2 CreateRsaCertificate()
    {
        using RSA key = RSA.Create(2048);
        var request = new CertificateRequest("CN=localhost", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using X509Certificate2 ephemeral = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
        return X509CertificateLoader.LoadPkcs12(ephemeral.Export(X509ContentType.Pkcs12), password: null);
    }

    private sealed class AcceptingVerifier : IServerCertificateVerifier
    {
        public static AcceptingVerifier Instance { get; } = new();

        public ServerCertificateVerdict Verify(ServerCertificateChain presented) => ServerCertificateVerdict.Accepted;
    }
}
