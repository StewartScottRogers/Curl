using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using Curl.Testing;
using static Curl.Tls.Rfc8448Messages;

namespace Curl.Tls;

/// <summary>
/// Replays RFC 8448 as the client: section 3 (the simple 1-RTT handshake) and section 5
/// (HelloRetryRequest), with the traces' randoms and ephemeral keys injected, so every
/// byte the client sends and every secret it installs is the trace's.
/// </summary>
[TestClass]
public sealed class Rfc8448ClientHandshakeTests
{
    // Section 3: {client} create an ephemeral x25519 key pair, and the ClientHello's random.
    private const string SimpleClientPrivateKey = "49af42ba7f7994852d713ef2784bcbcaa7911de26adc5642cb634540e7ea5005";
    private const string SimpleClientRandom = "cb34ecb1e78163ba1c38c6dacb196a6dffa21a8d9912ec18a2ef6283024dece7";

    // Section 5: {client} create an ephemeral x25519 key pair, then a P-256 key pair, and the random.
    private const string RetryClientX25519PrivateKey = "0ed02f8e8117efc75ca7ac32aa7e34eda64cdc0ddad154a5e85289f959f63204";
    private const string RetryClientP256PrivateKey = "ab5473467e19346ceb0a0414e41da21d4d2445bc3025afe97c4e8dc8d513da39";
    private const string RetryClientP256PublicKey =
        "04a6da7392ec591e17abfd535964b99894d13befb221b3def2ebe3830eac8f0151812677c4d6d2237e85cf01d6910cfb83954e76ba7352830534159897e8065780";

    private const string RetryClientRandom = "b0b1c5a5aa37c5919f2ed1d5c6fff7fcb7849716945a2b8cee9258a346677b6f";

    // The signature_algorithms both traces offer.
    private static readonly ushort[] TraceSignatureAlgorithms =
        [0x0403, 0x0503, 0x0603, 0x0203, 0x0804, 0x0805, 0x0806, 0x0401, 0x0501, 0x0601, 0x0201, 0x0402, 0x0502, 0x0602, 0x0202];

    private static readonly TlsExtension RenegotiationInfo = new(TlsExtensionType.RenegotiationInfo, [0x00]);
    private static readonly TlsExtension SessionTicket = new(TlsExtensionType.SessionTicket, []);
    private static readonly TlsExtension PskKeyExchangeModes = new(TlsExtensionType.PskKeyExchangeModes, [0x01, 0x01]);
    private static readonly TlsExtension RecordSizeLimit = new(TlsExtensionType.RecordSizeLimit, [0x40, 0x01]);

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public void SimpleHandshakeSendsTheTraceClientHello()
    {
        using Tls13ClientHandshake client = SimpleClient(new RecordingCertificateVerifier());

        Tls13HandshakeOutput output = client.Start();

        Assert.HasCount(1, output.BytesToSend);
        Assert.AreEqual(TlsEncryptionLevel.Initial, output.BytesToSend[0].Level);
        AssertHex(SimpleClientHello, output.BytesToSend[0].Bytes);
        Assert.IsEmpty(output.SecretsInstalled);
        Assert.IsFalse(output.IsComplete);
        Assert.IsNull(output.Failure);
    }

    [TestMethod]
    public void SimpleHandshakeInstallsTheTraceHandshakeSecretsOnTheServerHello()
    {
        using Tls13ClientHandshake client = SimpleClient(new RecordingCertificateVerifier());
        client.Start();

        Tls13HandshakeOutput output = client.Receive(TlsEncryptionLevel.Initial, Convert.FromHexString(SimpleServerHello));

        Assert.IsEmpty(output.BytesToSend);
        Assert.HasCount(2, output.SecretsInstalled);
        AssertSecret(output.SecretsInstalled[0], TlsEncryptionLevel.Handshake, TlsTrafficDirection.Read, "b67b7d690cc16c4e75e54213cb2d37b4e9c912bcded9105d42befd59d391ad38");
        AssertSecret(output.SecretsInstalled[1], TlsEncryptionLevel.Handshake, TlsTrafficDirection.Write, "b3eddb126e067f35a780b3abf45e2d8f3b1a950738f52e9600746a0e27a55a21");
        Assert.AreSame(Tls13CipherSuite.Aes128GcmSha256, client.CipherSuite);
        Assert.AreEqual(TlsNamedGroup.X25519, client.NegotiatedGroup);
    }

    [TestMethod]
    public void SimpleHandshakeAcceptsTheServerFlightAndSendsTheTraceFinished()
    {
        RecordingCertificateVerifier verifier = new();
        using Tls13ClientHandshake client = SimpleClient(verifier);
        client.Start();
        client.Receive(TlsEncryptionLevel.Initial, Convert.FromHexString(SimpleServerHello));

        Tls13HandshakeOutput output = client.Receive(
            TlsEncryptionLevel.Handshake,
            Convert.FromHexString(SimpleEncryptedExtensions + SimpleCertificate + SimpleCertificateVerify + SimpleServerFinished));

        Assert.IsNull(output.Failure);
        Assert.IsTrue(output.IsComplete);
        Assert.IsTrue(client.IsComplete);
        Assert.HasCount(1, output.BytesToSend);
        Assert.AreEqual(TlsEncryptionLevel.Handshake, output.BytesToSend[0].Level);
        AssertHex(SimpleClientFinished, output.BytesToSend[0].Bytes);
        Assert.HasCount(2, output.SecretsInstalled);
        AssertSecret(output.SecretsInstalled[0], TlsEncryptionLevel.Application, TlsTrafficDirection.Read, "a11af9f05531f856ad47116b45a950328204b4f44bfb6b3a4b4f1f3fcb631643");
        AssertSecret(output.SecretsInstalled[1], TlsEncryptionLevel.Application, TlsTrafficDirection.Write, "9e40646ce79a7f9dc05af8889bce6552875afa0b06df0087f792ebb7c17504a5");
        AssertHex("7df235f2031d2a051287d02b0241b0bfdaf86cc856231f2d5aba46c434ec196c", client.ResumptionMasterSecret!);
        AssertHex("fe22f881176eda18eb8f44529e6792c50c9a3f89452f68d8ae311b4309d3cf50", client.ExporterMasterSecret!);
        Assert.HasCount(1, verifier.Presented);
        Assert.AreEqual("server", verifier.Presented[0].HostName);
        Assert.IsNull(verifier.Presented[0].OcspResponse);
        Assert.HasCount(1, client.ServerCertificates);
        CollectionAssert.AreEqual(verifier.Presented[0].Certificates[0], client.ServerCertificates[0]);
        Assert.IsNull(client.ApplicationProtocol);
        Assert.IsFalse(client.ClientCertificateRequested);
        Assert.IsFalse(client.ClientCertificateSent);
    }

    [TestMethod]
    public void SimpleHandshakeTakesTheServerFlightOneByteAtATime()
    {
        using Tls13ClientHandshake client = SimpleClient(new RecordingCertificateVerifier());
        client.Start();
        client.Receive(TlsEncryptionLevel.Initial, Convert.FromHexString(SimpleServerHello));
        byte[] flight = Convert.FromHexString(SimpleEncryptedExtensions + SimpleCertificate + SimpleCertificateVerify + SimpleServerFinished);

        List<TlsHandshakeBytes> sent = [];
        using (Diagnostics.Phase("server flight one byte at a time"))
        {
            foreach (byte octet in flight)
            {
                sent.AddRange(client.Receive(TlsEncryptionLevel.Handshake, [octet]).BytesToSend);
            }
        }

        Diagnostics.Act("flights sent", sent.Count);
        Diagnostics.Assert("flights sent", 1, sent.Count);
        Assert.IsTrue(client.IsComplete);
        Assert.HasCount(1, sent);
        AssertHex(SimpleClientFinished, sent[0].Bytes);
    }

    [TestMethod]
    public void SimpleHandshakeKeepsTheNewSessionTicketThatFollows()
    {
        using Tls13ClientHandshake client = SimpleClient(new RecordingCertificateVerifier());
        client.Start();
        client.Receive(TlsEncryptionLevel.Initial, Convert.FromHexString(SimpleServerHello));
        client.Receive(TlsEncryptionLevel.Handshake, Convert.FromHexString(SimpleEncryptedExtensions + SimpleCertificate + SimpleCertificateVerify + SimpleServerFinished));

        Tls13HandshakeOutput output = client.Receive(TlsEncryptionLevel.Application, Hex(SimpleNewSessionTicket));

        Diagnostics.Act("failure", output.Failure);
        Diagnostics.Act("tickets received", client.ReceivedTickets.Count);
        Diagnostics.Assert("first ticket lifetime", 0x1eu, client.ReceivedTickets.Count == 0 ? null : client.ReceivedTickets[0].TicketLifetime);
        Assert.IsNull(output.Failure);
        Assert.IsTrue(output.IsComplete);
        Assert.HasCount(1, client.ReceivedTickets);
        Assert.AreEqual(0x1eu, client.ReceivedTickets[0].TicketLifetime);
    }

    [TestMethod]
    public void HelloRetryRequestHandshakeSendsBothTraceClientHellosAndTheTraceFinished()
    {
        using Tls13ClientHandshake client = RetryClient();

        Tls13HandshakeOutput first;
        Tls13HandshakeOutput second;
        Tls13HandshakeOutput keys;
        Tls13HandshakeOutput finished;
        using (Diagnostics.Phase("handshake with HelloRetryRequest"))
        {
            first = client.Start();
            second = client.Receive(TlsEncryptionLevel.Initial, Convert.FromHexString(HelloRetryRequest));
            keys = client.Receive(TlsEncryptionLevel.Initial, Convert.FromHexString(RetryServerHello));
            finished = client.Receive(
                TlsEncryptionLevel.Handshake,
                Convert.FromHexString(RetryEncryptedExtensions + SimpleCertificate + RetryCertificateVerify + RetryServerFinished));
        }

        Diagnostics.Act("second flight failure", second.Failure);
        Diagnostics.Act("final flight failure", finished.Failure);
        AssertHex(RetryFirstClientHello, first.BytesToSend[0].Bytes);
        Assert.IsNull(second.Failure);
        Assert.HasCount(1, second.BytesToSend);
        Assert.AreEqual(TlsEncryptionLevel.Initial, second.BytesToSend[0].Level);
        AssertHex(RetrySecondClientHello, second.BytesToSend[0].Bytes);
        Assert.IsEmpty(second.SecretsInstalled);
        Assert.HasCount(2, keys.SecretsInstalled);
        Assert.IsNull(finished.Failure);
        Assert.IsTrue(finished.IsComplete);
        AssertHex(RetryClientFinished, finished.BytesToSend[0].Bytes);
        Assert.AreEqual(TlsNamedGroup.Secp256r1, client.NegotiatedGroup);
    }

    private Tls13ClientHandshake SimpleClient(RecordingCertificateVerifier verifier)
    {
        Tls13ClientSettings settings = new()
        {
            ServerName = "server",
            CipherSuites = [0x1301, 0x1303, 0x1302],
            SupportedGroups =
            [
                TlsNamedGroup.X25519, TlsNamedGroup.Secp256r1, TlsNamedGroup.Secp384r1, TlsNamedGroup.Secp521r1,
                TlsNamedGroup.Ffdhe2048, TlsNamedGroup.Ffdhe3072, TlsNamedGroup.Ffdhe4096, TlsNamedGroup.Ffdhe6144, TlsNamedGroup.Ffdhe8192,
            ],
            KeyShareGroups = [TlsNamedGroup.X25519],
            SignatureAlgorithms = TraceSignatureAlgorithms,
            ExtensionOrder =
            [
                TlsExtensionType.ServerName, TlsExtensionType.RenegotiationInfo, TlsExtensionType.SupportedGroups, TlsExtensionType.SessionTicket,
                TlsExtensionType.KeyShare, TlsExtensionType.SupportedVersions, TlsExtensionType.SignatureAlgorithms,
                TlsExtensionType.PskKeyExchangeModes, TlsExtensionType.RecordSizeLimit,
            ],
            FixedExtensions = [RenegotiationInfo, SessionTicket, PskKeyExchangeModes, RecordSizeLimit],
        };
        ReplayTlsRandomSource random = new([Hex(SimpleClientRandom)], [new X25519KeyShare(Hex(SimpleClientPrivateKey))]);
        return new Tls13ClientHandshake(settings, random, verifier);
    }

    private Tls13ClientHandshake RetryClient()
    {
        Tls13ClientSettings settings = new()
        {
            ServerName = "server",
            CipherSuites = [0x1301, 0x1303, 0x1302],
            SupportedGroups = [TlsNamedGroup.X25519, TlsNamedGroup.Secp256r1, TlsNamedGroup.Secp384r1],
            KeyShareGroups = [TlsNamedGroup.X25519],
            SignatureAlgorithms = TraceSignatureAlgorithms,
            ExtensionOrder =
            [
                TlsExtensionType.ServerName, TlsExtensionType.RenegotiationInfo, TlsExtensionType.SupportedGroups, TlsExtensionType.KeyShare,
                TlsExtensionType.SupportedVersions, TlsExtensionType.SignatureAlgorithms, TlsExtensionType.Cookie,
                TlsExtensionType.PskKeyExchangeModes, TlsExtensionType.RecordSizeLimit, TlsExtensionType.Padding,
            ],
            FixedExtensions = [RenegotiationInfo, PskKeyExchangeModes, RecordSizeLimit],
        };
        byte[] publicKey = Hex(RetryClientP256PublicKey);
        ECParameters p256 = new()
        {
            Curve = ECCurve.NamedCurves.nistP256,
            D = Hex(RetryClientP256PrivateKey),
            Q = new ECPoint { X = publicKey[1..33], Y = publicKey[33..] },
        };
        ReplayTlsRandomSource random = new(
            [Hex(RetryClientRandom)],
            [new X25519KeyShare(Hex(RetryClientX25519PrivateKey)), new EcdhKeyShare(TlsNamedGroup.Secp256r1, ECDiffieHellman.Create(p256))]);
        return new Tls13ClientHandshake(settings, random, new RecordingCertificateVerifier());
    }

    private void AssertSecret(Tls13TrafficSecret secret, TlsEncryptionLevel level, TlsTrafficDirection direction, string expected, [CallerArgumentExpression(nameof(secret))] string label = "")
    {
        Assert.AreEqual(level, secret.Level);
        Assert.AreEqual(direction, secret.Direction);
        AssertHex(expected, secret.Secret, label);
    }

    // Writes the trace input as ARRANGE, labelled with the expression that names it.
    private byte[] Hex(string hex, [CallerArgumentExpression(nameof(hex))] string label = "") =>
        Diagnostics.ArrangeHex(label, hex);

    // Writes what the client produced as ACT and a DIFF against the trace's, labelled with
    // the expression that produced it.
    private void AssertHex(string expected, byte[] actual, [CallerArgumentExpression(nameof(actual))] string label = "") =>
        Assert.AreEqual(expected, Diagnostics.ActAndDiffHex(label, expected, actual));
}
