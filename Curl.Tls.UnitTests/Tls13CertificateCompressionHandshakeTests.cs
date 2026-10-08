using Curl.Testing;
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

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public void TheClientHelloOffersTheListedAlgorithmsAfterKeyShare()
    {
        using Tls13ClientHandshake client = Client(CompressingSettings);
        Diagnostics.Arrange("offered algorithms", string.Join(", ", CompressingSettings.CertificateCompressionAlgorithms));

        byte[] helloBytes = client.Start().BytesToSend[0].Bytes;
        ClientHello hello = ClientHello.Decode(HandshakeMessageReader.Read(helloBytes).Message!.Body).Value;

        TlsExtension[] extensions = [.. hello.Extensions];
        int keyShare = Array.FindIndex(extensions, extension => extension.Type == TlsExtensionType.KeyShare);
        Diagnostics.Bytes("ClientHello", helloBytes);
        Diagnostics.Act("extension order", string.Join(", ", extensions.Select(extension => extension.Type)));
        Diagnostics.Assert("extension after key_share", TlsExtensionType.CompressCertificate, extensions[keyShare + 1].Type);
        Diagnostics.Diff("compress_certificate body", Convert.FromHexString("0400010003"), extensions[keyShare + 1].Data);
        Assert.AreEqual(TlsExtensionType.CompressCertificate, extensions[keyShare + 1].Type);
        CollectionAssert.AreEqual(Convert.FromHexString("0400010003"), extensions[keyShare + 1].Data);
    }

    [TestMethod]
    public void WithoutAlgorithmsTheClientHelloOffersNone()
    {
        using Tls13ClientHandshake client = Client();
        Diagnostics.Arrange("offered algorithms", "none");

        ClientHello hello = ClientHello.Decode(HandshakeMessageReader.Read(client.Start().BytesToSend[0].Bytes).Message!.Body).Value;

        Diagnostics.Act("extension order", string.Join(", ", hello.Extensions.Select(extension => extension.Type)));
        Diagnostics.Assert("offers compress_certificate", false, hello.Extensions.Any(extension => extension.Type == TlsExtensionType.CompressCertificate));
        Assert.IsFalse(hello.Extensions.Any(extension => extension.Type == TlsExtensionType.CompressCertificate));
    }

    [TestMethod]
    [DataRow(CertificateCompressionAlgorithm.Zlib)]
    [DataRow(CertificateCompressionAlgorithm.Brotli)]
    [DataRow(CertificateCompressionAlgorithm.Zstd)]
    public void ACompressedCertificateWithEachAlgorithmCompletesWithTheSameChain(int algorithm)
    {
        Diagnostics.Arrange("algorithm", algorithm);
        RecordingCertificateVerifier plainVerifier = new();
        using Tls13ClientHandshake plainClient = Client(DefaultSettings, plainVerifier);
        bool plainComplete;
        using (Diagnostics.Phase("uncompressed handshake"))
        {
            plainComplete = Run(plainClient, new Tls13TestServer(Credential) { IssuerCertificates = [IssuerCertificate] }).IsComplete;
        }

        Diagnostics.Assert("uncompressed handshake complete", true, plainComplete);
        Assert.IsTrue(plainComplete);

        RecordingCertificateVerifier verifier = new();
        Tls13TestServer server = new(Credential)
        {
            IssuerCertificates = [IssuerCertificate],
            CompressCertificate = body => TestCertificateCompressor.Wrap((ushort)algorithm, body),
        };
        using Tls13ClientHandshake client = Client(CompressingSettings with { CertificateCompressionAlgorithms = [(ushort)algorithm] }, verifier);

        Tls13HandshakeOutput output;
        using (Diagnostics.Phase("compressed handshake"))
        {
            output = Run(client, server);
        }

        WriteOutput(output);
        Diagnostics.Act("certificates presented", verifier.Presented.Count == 0 ? 0 : verifier.Presented[0].Certificates.Count);
        Diagnostics.Assert("complete", true, output.IsComplete);
        Assert.IsNull(output.Failure);
        Assert.IsTrue(output.IsComplete);
        Assert.HasCount(2, verifier.Presented[0].Certificates);
        for (int index = 0; index < 2; index++)
        {
            Diagnostics.Diff($"certificate {index}", plainVerifier.Presented[0].Certificates[index], verifier.Presented[0].Certificates[index]);
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
        Diagnostics.Arrange("server", "requests a client certificate, sends a zstd CompressedCertificate");

        Tls13HandshakeOutput output;
        using (Diagnostics.Phase("handshake"))
        {
            output = Run(client, server);
        }

        WriteOutput(output);
        Diagnostics.Assert("complete", true, output.IsComplete);
        Assert.IsTrue(output.IsComplete);
    }

    [TestMethod]
    [DataRow(CertificateCompressionAlgorithm.Zlib, 1)]
    [DataRow(CertificateCompressionAlgorithm.Brotli, -1)]
    [DataRow(CertificateCompressionAlgorithm.Zstd, 1)]
    [DataRow(CertificateCompressionAlgorithm.Zstd, -1)]
    public void AWrongUncompressedLengthIsABadCertificate(int algorithm, int error)
    {
        Diagnostics.Arrange("algorithm", algorithm);
        Diagnostics.Arrange("uncompressed length error", error);
        AssertBadCertificate(body => TestCertificateCompressor.Wrap((ushort)algorithm, body) with { UncompressedLength = body.Length + error }, [CertificateCompressionAlgorithm.Zlib, CertificateCompressionAlgorithm.Brotli, CertificateCompressionAlgorithm.Zstd]);
    }

    [TestMethod]
    public void AnAlgorithmNotOfferedIsABadCertificate()
    {
        Diagnostics.Arrange("server algorithm", CertificateCompressionAlgorithm.Brotli);
        AssertBadCertificate(body => TestCertificateCompressor.Wrap(CertificateCompressionAlgorithm.Brotli, body), [CertificateCompressionAlgorithm.Zlib, CertificateCompressionAlgorithm.Zstd]);
    }

    [TestMethod]
    [DataRow(CertificateCompressionAlgorithm.Zlib)]
    [DataRow(CertificateCompressionAlgorithm.Brotli)]
    [DataRow(CertificateCompressionAlgorithm.Zstd)]
    public void CorruptCompressedDataIsABadCertificate(int algorithm)
    {
        Diagnostics.Arrange("algorithm", algorithm);
        Diagnostics.Arrange("corruption", "first 8 compressed bytes set to 0xFF");
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
        Diagnostics.Bytes("truncated CompressedCertificate", truncated);
        Diagnostics.Arrange("server flight", "Certificate replaced by the truncated CompressedCertificate");

        Tls13HandshakeOutput output;
        using (Diagnostics.Phase("handshake"))
        {
            output = Run(client, new Tls13TestServer(TestServerCredential.Ed25519()), replaceFlight: flight => Replace(flight, HandshakeType.Certificate, truncated));
        }

        WriteOutput(output);
        Diagnostics.Assert("alert", TlsAlertDescription.DecodeError, output.Failure?.Alert);
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
        Diagnostics.Arrange("offered algorithms", "none");
        Diagnostics.Arrange("server", "sends a zlib CompressedCertificate");

        Tls13HandshakeOutput output;
        using (Diagnostics.Phase("handshake"))
        {
            output = Run(client, server);
        }

        WriteOutput(output);
        Diagnostics.Assert("alert", TlsAlertDescription.UnexpectedMessage, output.Failure?.Alert);
        Assert.AreEqual(TlsAlertDescription.UnexpectedMessage, output.Failure!.Alert);
    }

    [TestMethod]
    public void SettingsRefuseAnAlgorithmTheClientCannotDecompress()
    {
        Diagnostics.Arrange("offered algorithms", "4 (unknown)");

        ArgumentException thrown = Assert.ThrowsExactly<ArgumentException>(() => Client(DefaultSettings with { CertificateCompressionAlgorithms = [4] }));

        Diagnostics.Act("thrown", $"{thrown.GetType().Name}: {thrown.Message}");
        Diagnostics.Assert("exception", nameof(ArgumentException), thrown.GetType().Name);
    }

    [TestMethod]
    public void SettingsRefuseAlgorithmsWithNoPlaceForTheExtension()
    {
        Diagnostics.Arrange("extension order", "supported_versions, key_share (no compress_certificate)");

        ArgumentException thrown = Assert.ThrowsExactly<ArgumentException>(() => Client(CompressingSettings with { ExtensionOrder = [TlsExtensionType.SupportedVersions, TlsExtensionType.KeyShare] }));

        Diagnostics.Act("thrown", $"{thrown.GetType().Name}: {thrown.Message}");
        Diagnostics.Assert("exception", nameof(ArgumentException), thrown.GetType().Name);
    }

    private void AssertBadCertificate(Func<byte[], CompressedCertificate> compress, ushort[] offered)
    {
        RecordingCertificateVerifier verifier = new();
        Tls13TestServer server = new(TestServerCredential.Ed25519()) { CompressCertificate = compress };
        using Tls13ClientHandshake client = Client(CompressingSettings with { CertificateCompressionAlgorithms = offered }, verifier);
        Diagnostics.Arrange("offered algorithms", string.Join(", ", offered));

        Tls13HandshakeOutput output;
        using (Diagnostics.Phase("handshake"))
        {
            output = Run(client, server);
        }

        WriteOutput(output);
        Diagnostics.Act("certificates presented to the verifier", verifier.Presented.Count);
        Diagnostics.Assert("alert", TlsAlertDescription.BadCertificate, output.Failure?.Alert);
        Diagnostics.Assert("complete", false, output.IsComplete);
        Assert.IsNotNull(output.Failure);
        Assert.AreEqual(TlsAlertDescription.BadCertificate, output.Failure.Alert);
        Assert.IsFalse(output.IsComplete);
        Assert.IsEmpty(verifier.Presented);
    }

    private void WriteOutput(Tls13HandshakeOutput output)
    {
        Diagnostics.Act("complete", output.IsComplete);
        Diagnostics.Act("failure alert", output.Failure?.Alert.ToString() ?? "none");
    }
}
