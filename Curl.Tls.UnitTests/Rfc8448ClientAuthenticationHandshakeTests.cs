using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Curl.Testing;
using static Curl.Tls.Rfc8448Messages;

namespace Curl.Tls;

/// <summary>
/// Replays RFC 8448 section 6 ("Client Authentication") as the client: the server asks for
/// a certificate and the client answers with the trace's RSA certificate, CertificateVerify
/// and Finished. The trace's CertificateVerify is RSA-PSS with a random salt over a key the
/// RFC does not publish, so a <see cref="TraceSignatureSigningKey" /> hands the client the
/// trace's signature and records the content it was asked to sign.
/// </summary>
[TestClass]
public sealed class Rfc8448ClientAuthenticationHandshakeTests
{
    // RFC 8448 section 6: {client} create an ephemeral x25519 key pair, and the ClientHello's random.
    private const string ClientPrivateKey = "c040b2bb8f3addd20fd4058c547003a3c6f9c1cd915d5e535c87d8d191aaf071";
    private const string ClientRandom = "6a472236328b83af40386d3a3e1f1ce624fa4ed89ab865a4ff0f4144ce3ae233";

    // RFC 8448 section 2: the RSA key behind section 3's server certificate (section 6's client key is not published).
    private const string Section2Modulus =
        "b4bb498f8279303d980836399b36c6988c0c68de55e1bdb826d3901a2461eafd2de49a91d015abbc9a95137ace6c1af19eaa6af98c7ced43120998e187a80ee0ccb0524b1b018c3e0b63264d449a6d38e22a5fda430846748030530ef0461c8ca9d9efbfae8ea6d1d03e2bd193eff0ab9a8002c47428a6d35a8d88d79f7f1e3f";

    private const string Section2PrivateExponent =
        "04dea705d43a6ea7209dd8072111a83c81e322a59278b33480641eaf7c0a6985b8e31c44f6de62e1b4c2309f6126e77b7c41e923314bbfa3881305dc1217f16c819ce538e922f369828d0e57195d8c8488460207b2faa726bcf708bbd7db7f679f893492fc2a622e08970aac441ce4e0c3088df25ae679233df8a3bda2ff9941";

    private const string Section2Prime1 = "e435fb7cc83737756dacea96ab7f59a2cc1069db7deb190e17e33a532b273f30a327aa0aaabc58cd67466af9845fadc675fe094af92c4bd1f2c1bc33dd2e0515";
    private const string Section2Prime2 = "cabd3bc0e0438664c8d4cc9f99977a94d9bbfead8e43870abae3f7eb8b4e0eee8af1d9b4719ba6196cf2cbbaeeebf8b3490afe9e9ffa74a88aa51fc645629303";
    private const string Section2Exponent1 = "3f57345c27fe1b687e6e761627b78b1b826433dd760fa0bea6a6acf39490aa1b47cda4869d68f584dd5b5029bd32093b8258661fe715025e5d70a45a08d3d319";
    private const string Section2Exponent2 = "183da01363bd2f2885cacbdc9964bf4764f1517636f86401286f71893c52ccfe40a6c23d0d086b47c6fb10d8fd1041e04def7e9a40ce957c417794e10412d139";
    private const string Section2Coefficient = "839ca9a085e4286b2c90e466997a2c681f21339aa3477814e4dec11833050ed50dd13cc038048a43c59b2acc416889c037665fe5afa605969f8c01dfa5ca969d";

    // RFC 8448 section 6: Certificate is header (4), context (1), list length (3), entry length (3), the DER, extensions (2).
    private static readonly byte[] ClientCertificateDer = Hex(ClientAuthenticationClientCertificate[22..^4]);

    // RFC 8448 section 6: CertificateVerify is header (4), scheme (2), signature length (2), the signature.
    private static readonly byte[] TraceClientSignature = Hex(ClientAuthenticationClientCertificateVerify[16..]);

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public void ClientAuthenticationHandshakeSendsTheTraceClientHelloAndAnswersTheCertificateRequestWithTheTraceFlight()
    {
        TraceSignatureSigningKey key = new(TraceClientSignature);
        using Tls13ClientHandshake client = ClientAuthenticationClient(key);

        Tls13HandshakeOutput hello;
        Tls13HandshakeOutput keys;
        Tls13HandshakeOutput flight;
        using (Diagnostics.Phase("handshake"))
        {
            hello = client.Start();
            keys = client.Receive(TlsEncryptionLevel.Initial, Hex(ClientAuthenticationServerHello));
            flight = client.Receive(
                TlsEncryptionLevel.Handshake,
                Hex(ClientAuthenticationEncryptedExtensions + ClientAuthenticationCertificateRequest + ClientAuthenticationServerCertificate
                    + ClientAuthenticationServerCertificateVerify + ClientAuthenticationServerFinished));
        }

        string clientFlight = string.Concat(flight.BytesToSend.Select(sent => Convert.ToHexStringLower(sent.Bytes)));
        Diagnostics.Act("failure", flight.Failure);
        Diagnostics.Diff(
            "client Certificate, CertificateVerify and Finished",
            ClientAuthenticationClientCertificate + ClientAuthenticationClientCertificateVerify + ClientAuthenticationClientFinished,
            clientFlight);

        // RFC 8448 section 6: {client} construct a ClientHello handshake message.
        Assert.HasCount(1, hello.BytesToSend);
        AssertHex(ClientAuthenticationClientHello, hello.BytesToSend[0].Bytes);

        // RFC 8448 section 6: {server} derive secret "tls13 s hs traffic" and "tls13 c hs traffic".
        Assert.HasCount(2, keys.SecretsInstalled);
        AssertSecret(keys.SecretsInstalled[0], TlsEncryptionLevel.Handshake, TlsTrafficDirection.Read, "8b02d3c00442a2722c4098ebe8675b23e801510f0d7ed778d8eb0b8f42a19a5e");
        AssertSecret(keys.SecretsInstalled[1], TlsEncryptionLevel.Handshake, TlsTrafficDirection.Write, "cec7a30c6872070f22a7eeb065768db67c45e29533db879908ce6dc66f5911de");

        // RFC 8448 section 6: {client} construct a Certificate, a CertificateVerify and a Finished handshake message.
        Assert.IsNull(flight.Failure);
        Assert.IsTrue(flight.IsComplete);
        Assert.AreEqual(
            ClientAuthenticationClientCertificate + ClientAuthenticationClientCertificateVerify + ClientAuthenticationClientFinished,
            clientFlight);
        Assert.IsTrue(flight.BytesToSend.All(sent => sent.Level == TlsEncryptionLevel.Handshake));

        // RFC 8448 section 6: {server} derive secret "tls13 s ap traffic" and "tls13 c ap traffic".
        Assert.HasCount(2, flight.SecretsInstalled);
        AssertSecret(flight.SecretsInstalled[0], TlsEncryptionLevel.Application, TlsTrafficDirection.Read, "c49a91faf57f8c545d5048a015bf849ff63942e4a7edcd319f8b438a97c52e21");
        AssertSecret(flight.SecretsInstalled[1], TlsEncryptionLevel.Application, TlsTrafficDirection.Write, "73c2e890fa8d067258d6d50fa92fe456b098cf00d9727eed91e8892ef4e6f860");
        AssertHex("052e39795e5f2be6e4e0974cfdd86c6a7afe3e57e5589810a3cccf642958beb2", client.ExporterMasterSecret!);
        AssertHex("1006dccbf40eb4eb978bff0392a9e452a4fbad58aa14784d5a241c6b49daccfb", client.ResumptionMasterSecret!);
        Assert.IsTrue(client.ClientCertificateRequested);
        Assert.IsTrue(client.ClientCertificateSent);
        Assert.AreEqual(HashAlgorithmName.SHA256, key.SignedHash);
    }

    [TestMethod]
    public void ClientAuthenticationHandshakeSignsTheContentTheTraceSignatureCovers()
    {
        TraceSignatureSigningKey key = new(TraceClientSignature);
        DriveClientAuthenticationHandshake(key);

        // RFC 8448 section 6: the client's CertificateVerify signature verifies under the client certificate's key
        // over the content the client asked its key to sign, so that content is the trace's.
        using X509Certificate2 certificate = X509CertificateLoader.LoadCertificate(ClientCertificateDer);
        using RSA publicKey = certificate.GetRSAPublicKey()!;
        bool verified = publicKey.VerifyData(key.SignedContent!, TraceClientSignature, HashAlgorithmName.SHA256, RSASignaturePadding.Pss);
        Diagnostics.Assert("trace signature verifies over the signed content", true, verified);
        Assert.IsTrue(verified);
    }

    [TestMethod]
    public void RsaTlsSigningKeySignsTheTraceCertificateVerifyContentWithRsaPssSha256()
    {
        TraceSignatureSigningKey trace = new(TraceClientSignature);
        DriveClientAuthenticationHandshake(trace);
        using RSA privateKey = RSA.Create(new RSAParameters
        {
            Modulus = Hex(Section2Modulus),
            Exponent = [0x01, 0x00, 0x01],
            D = Hex(Section2PrivateExponent),
            P = Hex(Section2Prime1),
            Q = Hex(Section2Prime2),
            DP = Hex(Section2Exponent1),
            DQ = Hex(Section2Exponent2),
            InverseQ = Hex(Section2Coefficient),
        });

        Diagnostics.Arrange("private key", "RFC 8448 section 2's RSA key");
        byte[] signature = new RsaTlsSigningKey(privateKey).Sign(TlsSignatureScheme.RsaPssRsaeSha256, trace.SignedContent!);
        Diagnostics.Act("signature length", signature.Length);

        // RFC 8448 section 6's client key is not published, so section 2's RSA key signs the section 6 content
        // and section 3's server certificate, which carries that key, verifies it.
        using X509Certificate2 certificate = X509CertificateLoader.LoadCertificate(Body(SimpleCertificate, HandshakeType.Certificate)[7..^2]);
        using RSA publicKey = certificate.GetRSAPublicKey()!;
        bool verified = publicKey.VerifyData(trace.SignedContent!, signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pss);
        Diagnostics.Assert("signature verifies under section 3's server certificate", true, verified);
        Assert.HasCount(128, signature);
        Assert.IsTrue(verified);
    }

    private void DriveClientAuthenticationHandshake(TraceSignatureSigningKey key)
    {
        using Tls13ClientHandshake client = ClientAuthenticationClient(key);
        Tls13HandshakeOutput flight;
        using (Diagnostics.Phase("handshake"))
        {
            client.Start();
            client.Receive(TlsEncryptionLevel.Initial, Hex(ClientAuthenticationServerHello));
            flight = client.Receive(
                TlsEncryptionLevel.Handshake,
                Hex(ClientAuthenticationEncryptedExtensions + ClientAuthenticationCertificateRequest + ClientAuthenticationServerCertificate
                    + ClientAuthenticationServerCertificateVerify + ClientAuthenticationServerFinished));
        }

        Diagnostics.Act("handshake complete", flight.IsComplete);
        Diagnostics.Bytes("content the client asked its key to sign", key.SignedContent);
        Assert.IsTrue(flight.IsComplete);
    }

    private Tls13ClientHandshake ClientAuthenticationClient(TlsSigningKey key)
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
            SignatureAlgorithms = [0x0403, 0x0503, 0x0603, 0x0203, 0x0804, 0x0805, 0x0806, 0x0401, 0x0501, 0x0601, 0x0201, 0x0402, 0x0502, 0x0602, 0x0202],
            ExtensionOrder =
            [
                TlsExtensionType.ServerName, TlsExtensionType.RenegotiationInfo, TlsExtensionType.SupportedGroups, TlsExtensionType.KeyShare,
                TlsExtensionType.SupportedVersions, TlsExtensionType.SignatureAlgorithms, TlsExtensionType.PskKeyExchangeModes,
                TlsExtensionType.RecordSizeLimit,
            ],
            FixedExtensions =
            [
                new TlsExtension(TlsExtensionType.RenegotiationInfo, [0x00]),
                new TlsExtension(TlsExtensionType.PskKeyExchangeModes, [0x01, 0x01]),
                new TlsExtension(TlsExtensionType.RecordSizeLimit, [0x40, 0x01]),
            ],
            ClientCertificate = new TlsClientCertificate([ClientCertificateDer], key),
        };
        ReplayTlsRandomSource random = new([Diagnostics.ArrangeHex(nameof(ClientRandom), ClientRandom)], [new X25519KeyShare(Diagnostics.ArrangeHex(nameof(ClientPrivateKey), ClientPrivateKey))]);
        return new Tls13ClientHandshake(settings, random, new RecordingCertificateVerifier());
    }

    private void AssertSecret(Tls13TrafficSecret secret, TlsEncryptionLevel level, TlsTrafficDirection direction, string expected, [CallerArgumentExpression(nameof(secret))] string label = "")
    {
        Assert.AreEqual(level, secret.Level);
        Assert.AreEqual(direction, secret.Direction);
        AssertHex(expected, secret.Secret, label);
    }

    private static byte[] Hex(string hex) => Convert.FromHexString(hex);

    private void AssertHex(string expected, byte[] actual, [CallerArgumentExpression(nameof(actual))] string label = "") =>
        Assert.AreEqual(expected, Diagnostics.ActAndDiffHex(label, expected, actual));

    /// <summary>
    /// An RSA key certified as <c>rsaEncryption</c> that signs only with RSA-PSS by returning
    /// RFC 8448 section 6's signature, and records the hash and content it was asked to sign.
    /// </summary>
    private sealed class TraceSignatureSigningKey(byte[] traceSignature) : TlsSigningKey
    {
        public HashAlgorithmName SignedHash { get; private set; }

        public byte[]? SignedContent { get; private set; }

        private protected override bool Fits(TlsSignatureRule rule) =>
            rule.Kind == TlsSignatureKind.RsaPss && rule.KeyOid == TlsSignatureScheme.RsaEncryptionOid;

        private protected override byte[] Sign(TlsSignatureRule rule, byte[] content)
        {
            SignedHash = rule.Hash;
            SignedContent = content;
            return traceSignature;
        }
    }
}
