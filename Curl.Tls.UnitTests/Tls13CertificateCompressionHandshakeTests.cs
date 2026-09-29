using static Curl.Tls.HandshakeDriver;

namespace Curl.Tls;

/// <summary>
/// RFC 8879 in the TLS 1.3 client handshake: the ClientHello offers
/// <c>compress_certificate</c>, a CompressedCertificate with each algorithm completes the
/// handshake and hands the verifier the chain an uncompressed Certificate would, and a
/// wrong length, an algorithm not offered or corrupt data is <c>bad_certificate</c>.
/// </summary>
[TestClass]
public sealed class Tls13CertificateCompressionHandshakeTests
{
    private static readonly Tls13ClientSettings CompressingSettings =
        DefaultSettings with { CertificateCompressionAlgorithms = ClientHelloProfile.OpenSsl.CertificateCompressionAlgorithms };

    private static readonly TestServerCredential Credential = TestServerCredential.Ed25519();

    private static readonly byte[] IssuerCertificate = TestServerCredential.Ed25519().Certificate;

    [TestMethod]
    public void TheClientHelloOffersTheListedAlgorithmsAfterKeyShare()
    {
        using Tls13ClientHandshake client = Client(CompressingSettings);

        byte[] helloBytes = client.Start().BytesToSend[0].Bytes;
        ClientHello hello = ClientHello.Decode(HandshakeMessageReader.Read(helloBytes).Message!.Body).Value;

        TlsExtension[] extensions = [.. hello.Extensions];
        int keyShare = Array.FindIndex(extensions, extension => extension.Type == TlsExtensionType.KeyShare);
        Assert.AreEqual(TlsExtensionType.CompressCertificate, extensions[keyShare + 1].Type);
        CollectionAssert.AreEqual(Convert.FromHexString("0400010003"), extensions[keyShare + 1].Data);
    }

    [TestMethod]
    public void WithoutAlgorithmsTheClientHelloOffersNone()
    {
        using Tls13ClientHandshake client = Client();

        ClientHello hello = ClientHello.Decode(HandshakeMessageReader.Read(client.Start().BytesToSend[0].Bytes).Message!.Body).Value;

        Assert.IsFalse(hello.Extensions.Any(extension => extension.Type == TlsExtensionType.CompressCertificate));
    }

    [TestMethod]
    [DataRow(CertificateCompressionAlgorithm.Zlib)]
    [DataRow(CertificateCompressionAlgorithm.Brotli)]
    [DataRow(CertificateCompressionAlgorithm.Zstd)]
    public void ACompressedCertificateWithEachAlgorithmCompletesWithTheSameChain(int algorithm)
    {
        RecordingCertificateVerifier plainVerifier = new();
        using Tls13ClientHandshake plainClient = Client(DefaultSettings, plainVerifier);
        Assert.IsTrue(Run(plainClient, new Tls13TestServer(Credential) { IssuerCertificates = [IssuerCertificate] }).IsComplete);

        RecordingCertificateVerifier verifier = new();
        Tls13TestServer server = new(Credential)
        {
            IssuerCertificates = [IssuerCertificate],
            CompressCertificate = body => TestCertificateCompressor.Wrap((ushort)algorithm, body),
        };
        using Tls13ClientHandshake client = Client(CompressingSettings with { CertificateCompressionAlgorithms = [(ushort)algorithm] }, verifier);

        Tls13HandshakeOutput output = Run(client, server);

        Assert.IsNull(output.Failure);
        Assert.IsTrue(output.IsComplete);
        Assert.HasCount(2, verifier.Presented[0].Certificates);
        for (int index = 0; index < 2; index++)
        {
            CollectionAssert.AreEqual(plainVerifier.Presented[0].Certificates[index], verifier.Presented[0].Certificates[index]);
        }
    }

    [TestMethod]
    public void ACompressedCertificateCompletesAfterACertificateRequest()
    {
        Tls13TestServer server = new(TestServerCredential.Ed25519())
        {
            RequestClientCertificate = true,
            CompressCertificate = body => TestCertificateCompressor.Wrap(CertificateCompressionAlgorithm.Zstd, body),
        };
        using Tls13ClientHandshake client = Client(CompressingSettings);

        Assert.IsTrue(Run(client, server).IsComplete);
    }

    [TestMethod]
    [DataRow(CertificateCompressionAlgorithm.Zlib, 1)]
    [DataRow(CertificateCompressionAlgorithm.Brotli, -1)]
    [DataRow(CertificateCompressionAlgorithm.Zstd, 1)]
    [DataRow(CertificateCompressionAlgorithm.Zstd, -1)]
    public void AWrongUncompressedLengthIsABadCertificate(int algorithm, int error)
    {
        AssertBadCertificate(body => TestCertificateCompressor.Wrap((ushort)algorithm, body) with { UncompressedLength = body.Length + error }, [CertificateCompressionAlgorithm.Zlib, CertificateCompressionAlgorithm.Brotli, CertificateCompressionAlgorithm.Zstd]);
    }

    [TestMethod]
    public void AnAlgorithmNotOfferedIsABadCertificate()
    {
        AssertBadCertificate(body => TestCertificateCompressor.Wrap(CertificateCompressionAlgorithm.Brotli, body), [CertificateCompressionAlgorithm.Zlib, CertificateCompressionAlgorithm.Zstd]);
    }

    [TestMethod]
    [DataRow(CertificateCompressionAlgorithm.Zlib)]
    [DataRow(CertificateCompressionAlgorithm.Brotli)]
    [DataRow(CertificateCompressionAlgorithm.Zstd)]
    public void CorruptCompressedDataIsABadCertificate(int algorithm)
    {
        AssertBadCertificate(
            body =>
            {
                CompressedCertificate message = TestCertificateCompressor.Wrap((ushort)algorithm, body);
                byte[] corrupt = [.. message.CompressedCertificateMessage];
                Array.Fill<byte>(corrupt, 0xFF, 0, Math.Min(8, corrupt.Length));
                return message with { CompressedCertificateMessage = corrupt };
            },
            [(ushort)algorithm]);
    }

    [TestMethod]
    public void AMalformedCompressedCertificateIsADecodeError()
    {
        using Tls13ClientHandshake client = Client(CompressingSettings);
        byte[] truncated = new HandshakeMessage(HandshakeType.CompressedCertificate, [0, 1, 0]).Encode();

        Tls13HandshakeOutput output = Run(client, new Tls13TestServer(TestServerCredential.Ed25519()), replaceFlight: flight => Replace(flight, HandshakeType.Certificate, truncated));

        Assert.AreEqual(TlsAlertDescription.DecodeError, output.Failure!.Alert);
    }

    [TestMethod]
    public void ACompressedCertificateWithoutAnOfferIsUnexpected()
    {
        Tls13TestServer server = new(TestServerCredential.Ed25519())
        {
            CompressCertificate = body => TestCertificateCompressor.Wrap(CertificateCompressionAlgorithm.Zlib, body),
        };
        using Tls13ClientHandshake client = Client();

        Tls13HandshakeOutput output = Run(client, server);

        Assert.AreEqual(TlsAlertDescription.UnexpectedMessage, output.Failure!.Alert);
    }

    [TestMethod]
    public void SettingsRefuseAnAlgorithmTheClientCannotDecompress()
    {
        Assert.ThrowsExactly<ArgumentException>(() => Client(DefaultSettings with { CertificateCompressionAlgorithms = [4] }));
    }

    [TestMethod]
    public void SettingsRefuseAlgorithmsWithNoPlaceForTheExtension()
    {
        Assert.ThrowsExactly<ArgumentException>(() => Client(CompressingSettings with { ExtensionOrder = [TlsExtensionType.SupportedVersions, TlsExtensionType.KeyShare] }));
    }

    private static void AssertBadCertificate(Func<byte[], CompressedCertificate> compress, ushort[] offered)
    {
        RecordingCertificateVerifier verifier = new();
        Tls13TestServer server = new(TestServerCredential.Ed25519()) { CompressCertificate = compress };
        using Tls13ClientHandshake client = Client(CompressingSettings with { CertificateCompressionAlgorithms = offered }, verifier);

        Tls13HandshakeOutput output = Run(client, server);

        Assert.IsNotNull(output.Failure);
        Assert.AreEqual(TlsAlertDescription.BadCertificate, output.Failure.Alert);
        Assert.IsFalse(output.IsComplete);
        Assert.IsEmpty(verifier.Presented);
    }
}
