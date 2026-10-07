using System.Formats.Asn1;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

using Curl.Cryptography;
using Curl.Protocol.Abstractions;
using Curl.Testing;
using Curl.Tls;

using CertificateRequest = System.Security.Cryptography.X509Certificates.CertificateRequest;

namespace Curl.Networking;

/// <summary>
/// Pins <see cref="HandBuiltPrivateKeyReader" /> and the <c>--cert</c> loading it serves
/// (ADR-0301): an Ed25519, Ed448 or ML-DSA certificate with its PKCS #8 key, PEM or DER, loads
/// as a <see cref="HandBuiltKeyCertificate" /> whose signing key the hand-built client signs
/// with; an ML-DSA key loads in each of RFC 9881's three forms; and a malformed key, one of
/// another algorithm or one that is not the certificate's is the exit 43 an unusable
/// <c>--key</c> already gives.
/// </summary>
[TestClass]
public sealed class HandBuiltPrivateKeyReaderTests
{
    private static readonly byte[] Seed32 = [.. Enumerable.Range(1, 32).Select(value => (byte)value)];

    private static readonly byte[] Seed57 = [.. Enumerable.Range(1, 57).Select(value => (byte)value)];

    private static readonly Lazy<(RSA Key, X509Certificate2 Certificate)> Issuer = new(CreateIssuer);

    private readonly string _directory = Path.Combine(Path.GetTempPath(), "curl-handbuilt-key-" + Guid.NewGuid().ToString("N"));

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestCleanup]
    public void DeleteFiles()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [TestMethod]
    [DataRow(HandBuiltPrivateKeyReader.Ed25519Oid, true)]
    [DataRow(HandBuiltPrivateKeyReader.Ed448Oid, true)]
    [DataRow(HandBuiltPrivateKeyReader.MlDsa44Oid, true)]
    [DataRow(HandBuiltPrivateKeyReader.MlDsa65Oid, true)]
    [DataRow(HandBuiltPrivateKeyReader.MlDsa87Oid, true)]
    [DataRow("1.2.840.113549.1.1.1", false)]
    [DataRow(null, false)]
    public void Reads_TakesEd25519Ed448AndMlDsaOnly(string? keyAlgorithmOid, bool expected)
    {
        Diagnostics.Arrange("key algorithm OID", keyAlgorithmOid ?? "null");

        var reads = HandBuiltPrivateKeyReader.Reads(keyAlgorithmOid);

        Diagnostics.Act("reads", reads);
        Diagnostics.Assert("reads", expected, reads);
        Assert.AreEqual(expected, reads);
    }

    [TestMethod]
    [DataRow(HandBuiltPrivateKeyReader.Ed25519Oid, TlsSignatureScheme.Ed25519, false)]
    [DataRow(HandBuiltPrivateKeyReader.Ed448Oid, TlsSignatureScheme.Ed448, false)]
    [DataRow(HandBuiltPrivateKeyReader.MlDsa44Oid, TlsSignatureScheme.MlDsa44, false)]
    [DataRow(HandBuiltPrivateKeyReader.MlDsa65Oid, TlsSignatureScheme.MlDsa65, false)]
    [DataRow(HandBuiltPrivateKeyReader.MlDsa87Oid, TlsSignatureScheme.MlDsa87, false)]
    [DataRow(HandBuiltPrivateKeyReader.Ed25519Oid, TlsSignatureScheme.Ed25519, true)]
    [DataRow(HandBuiltPrivateKeyReader.MlDsa65Oid, TlsSignatureScheme.MlDsa65, true)]
    public void LoadAsOpenSslBuild_WithAHandBuiltKey_GivesAClientCertificateThatSignsWithItsScheme(string keyAlgorithmOid, int scheme, bool asDer)
    {
        var (publicKey, privateKeyInfo) = KeyPair(keyAlgorithmOid);
        using var certificate = CertificateFor(keyAlgorithmOid, publicKey);
        Diagnostics.Arrange("key algorithm OID", keyAlgorithmOid);
        Diagnostics.Arrange("signature scheme", $"0x{scheme:x4}");

        var (loaded, failure) = Load(certificate, privateKeyInfo, asDer);

        Diagnostics.Assert("failure is null", true, failure is null);
        Assert.IsNull(failure);
        using (loaded)
        {
            var clientCertificate = HandBuiltTlsProvider.ToTlsClientCertificate(loaded)!;
            Diagnostics.Act("signing key", clientCertificate.SigningKey.GetType().Name);
            Diagnostics.Assert("signs with the scheme", true, clientCertificate.SigningKey.CanSign((ushort)scheme));
            Diagnostics.Assert("signs with RSA-PSS", false, clientCertificate.SigningKey.CanSign(TlsSignatureScheme.RsaPssRsaeSha256));
            Diagnostics.Assert("chain is the certificate", true, certificate.RawData.AsSpan().SequenceEqual(clientCertificate.CertificateChain.Single()));
            Assert.IsTrue(clientCertificate.SigningKey.CanSign((ushort)scheme));
            Assert.IsFalse(clientCertificate.SigningKey.CanSign(TlsSignatureScheme.RsaPssRsaeSha256));
            CollectionAssert.AreEqual(certificate.RawData, clientCertificate.CertificateChain.Single());
            Assert.IsNotEmpty(clientCertificate.SigningKey.Sign((ushort)scheme, [1, 2, 3]));
        }
    }

    [TestMethod]
    [DataRow("seed")]
    [DataRow("expandedKey")]
    [DataRow("both")]
    public void LoadAsOpenSslBuild_WithAnMlDsaKeyInEachForm_LoadsIt(string form)
    {
        using var key = MlDsa.GenerateKey(MlDsaParameterSet.MlDsa44, Seed32);
        using var certificate = CertificateFor(HandBuiltPrivateKeyReader.MlDsa44Oid, PublicKeyOf(key));
        var privateKey = form switch
        {
            "seed" => MlDsaSeedForm(Seed32),
            "expandedKey" => OctetString(ExpandedKeyOf(key)),
            _ => MlDsaBothForm(Seed32, ExpandedKeyOf(key)),
        };

        Diagnostics.Arrange("ML-DSA-44 private key form", form);

        var (loaded, failure) = Load(certificate, PrivateKeyInfo(HandBuiltPrivateKeyReader.MlDsa44Oid, privateKey), asDer: false);

        Diagnostics.Assert("failure is null", true, failure is null);
        Assert.IsNull(failure);
        using (loaded)
        {
            Diagnostics.Assert("signs with ML-DSA-44", true, HandBuiltTlsProvider.ToTlsClientCertificate(loaded)!.SigningKey.CanSign(TlsSignatureScheme.MlDsa44));
            Assert.IsTrue(HandBuiltTlsProvider.ToTlsClientCertificate(loaded)!.SigningKey.CanSign(TlsSignatureScheme.MlDsa44));
        }
    }

    [TestMethod]
    [DataRow("both with another expanded key")]
    [DataRow("both with a trailing field")]
    [DataRow("seed of 31 bytes")]
    [DataRow("expanded key of the wrong length")]
    [DataRow("an INTEGER instead of a key")]
    [DataRow("a trailing field after the key")]
    [DataRow("another key pair")]
    public void LoadAsOpenSslBuild_WithAMalformedMlDsaKey_FailsAsAnUnusableKey(string malformation)
    {
        using var key = MlDsa.GenerateKey(MlDsaParameterSet.MlDsa44, Seed32);
        using var other = MlDsa.GenerateKey(MlDsaParameterSet.MlDsa44);
        using var certificate = CertificateFor(HandBuiltPrivateKeyReader.MlDsa44Oid, PublicKeyOf(key));
        var privateKey = malformation switch
        {
            "both with another expanded key" => MlDsaBothForm(Seed32, ExpandedKeyOf(other)),
            "both with a trailing field" => Sequence(writer =>
            {
                writer.WriteOctetString(Seed32);
                writer.WriteOctetString(ExpandedKeyOf(key));
                writer.WriteInteger(0);
            }),
            "seed of 31 bytes" => MlDsaSeedForm(Seed32[..31]),
            "expanded key of the wrong length" => OctetString(new byte[10]),
            "an INTEGER instead of a key" => Integer(),
            "a trailing field after the key" => [.. MlDsaSeedForm(Seed32), .. Integer()],
            _ => OctetString(ExpandedKeyOf(other)),
        };
        Diagnostics.Arrange("ML-DSA-44 key malformation", malformation);

        AssertUnusable(certificate, PrivateKeyInfo(HandBuiltPrivateKeyReader.MlDsa44Oid, privateKey));
    }

    [TestMethod]
    [DataRow("not DER")]
    [DataRow("a trailing field after the PrivateKeyInfo")]
    [DataRow("algorithm parameters")]
    [DataRow("another algorithm")]
    [DataRow("a CurvePrivateKey of the wrong length")]
    [DataRow("a CurvePrivateKey with a trailing field")]
    [DataRow("another key pair")]
    public void LoadAsOpenSslBuild_WithAMalformedEd25519Key_FailsAsAnUnusableKey(string malformation)
    {
        var (publicKey, privateKeyInfo) = KeyPair(HandBuiltPrivateKeyReader.Ed25519Oid);
        using var certificate = CertificateFor(HandBuiltPrivateKeyReader.Ed25519Oid, publicKey);
        var otherSeed = Seed32.Reverse().ToArray();
        var malformed = malformation switch
        {
            "not DER" => [1, 2, 3],
            "a trailing field after the PrivateKeyInfo" => [.. privateKeyInfo, .. Integer()],
            "algorithm parameters" => Sequence(writer =>
            {
                writer.WriteInteger(0);
                using (writer.PushSequence())
                {
                    writer.WriteObjectIdentifier(HandBuiltPrivateKeyReader.Ed25519Oid);
                    writer.WriteNull();
                }

                writer.WriteOctetString(OctetString(Seed32));
            }),
            "another algorithm" => PrivateKeyInfo(HandBuiltPrivateKeyReader.Ed448Oid, OctetString(Seed57)),
            "a CurvePrivateKey of the wrong length" => PrivateKeyInfo(HandBuiltPrivateKeyReader.Ed25519Oid, OctetString(Seed57)),
            "a CurvePrivateKey with a trailing field" => PrivateKeyInfo(HandBuiltPrivateKeyReader.Ed25519Oid, [.. OctetString(Seed32), .. Integer()]),
            _ => PrivateKeyInfo(HandBuiltPrivateKeyReader.Ed25519Oid, OctetString(otherSeed)),
        };
        Diagnostics.Arrange("Ed25519 key malformation", malformation);

        AssertUnusable(certificate, malformed);
    }

    [TestMethod]
    [DataRow("no PEM at all")]
    [DataRow("only an RSA PRIVATE KEY")]
    public void LoadAsOpenSslBuild_WithAPemFileHoldingNoPrivateKey_FailsAsAnUnusableKey(string contents)
    {
        var (publicKey, privateKeyInfo) = KeyPair(HandBuiltPrivateKeyReader.Ed25519Oid);
        using var certificate = CertificateFor(HandBuiltPrivateKeyReader.Ed25519Oid, publicKey);
        var keyFile = WriteFile("key.pem", contents == "no PEM at all"
            ? "not a key"
            : PemEncoding.WriteString("RSA PRIVATE KEY", privateKeyInfo));
        Diagnostics.Arrange("key file contents", contents);

        var (loaded, failure) = ClientCertificateLoader.LoadAsOpenSslBuild(WriteFile("cert.pem", certificate.ExportCertificatePem()), null, keyFile, null, null);

        ActLoad(loaded, failure);
        Diagnostics.Assert("loaded is null", true, loaded is null);
        Diagnostics.Assert("exit code", CurlExitCode.BadFunctionArgument, failure?.ExitCode);
        Assert.IsNull(loaded);
        Assert.AreEqual(CurlExitCode.BadFunctionArgument, failure!.ExitCode);
    }

    [TestMethod]
    public void LoadAsOpenSslBuild_WithAPemKeyAfterAnotherBlock_FindsThePrivateKey()
    {
        var (publicKey, privateKeyInfo) = KeyPair(HandBuiltPrivateKeyReader.Ed448Oid);
        using var certificate = CertificateFor(HandBuiltPrivateKeyReader.Ed448Oid, publicKey);
        var keyFile = WriteFile("key.pem", certificate.ExportCertificatePem() + "\n" + PemEncoding.WriteString("PRIVATE KEY", privateKeyInfo));
        Diagnostics.Arrange("key algorithm", "Ed448");
        Diagnostics.Arrange("--cert file", "a CERTIFICATE block, then a PRIVATE KEY block; no --key");

        var (loaded, failure) = ClientCertificateLoader.LoadAsOpenSslBuild(keyFile, null, null, null, null);

        ActLoad(loaded, failure);
        Diagnostics.Assert("failure is null", true, failure is null);
        Assert.IsNull(failure);
        using (loaded)
        {
            Diagnostics.Assert("signing key", nameof(Ed448TlsSigningKey), (loaded as HandBuiltKeyCertificate)?.SigningKey.GetType().Name);
            Assert.IsInstanceOfType<Ed448TlsSigningKey>(((HandBuiltKeyCertificate)loaded!).SigningKey);
        }
    }

    [TestMethod]
    [DataRow(HandBuiltPrivateKeyReader.Ed25519Oid, TlsSignatureScheme.Ed25519)]
    [DataRow(HandBuiltPrivateKeyReader.MlDsa44Oid, TlsSignatureScheme.MlDsa44)]
    public void LoadAsOpenSslBuild_WithAnEncryptedPemKeyAndItsPassphrase_GivesAClientCertificateThatSignsWithItsScheme(string keyAlgorithmOid, int scheme)
    {
        var (publicKey, privateKeyInfo) = KeyPair(keyAlgorithmOid);
        using var certificate = CertificateFor(keyAlgorithmOid, publicKey);
        var keyFile = WriteEncryptedKey(privateKeyInfo);
        Diagnostics.Arrange("key algorithm OID", keyAlgorithmOid);
        Diagnostics.Arrange("key file", "ENCRYPTED PRIVATE KEY, AES-256-CBC, HMAC-SHA256 PBKDF2");
        Diagnostics.Arrange("passphrase", "the right one");

        X509Certificate2? loaded;
        ConnectResult? failure;
        using (Diagnostics.Phase("decrypt and load"))
        {
            (loaded, failure) = ClientCertificateLoader.LoadAsOpenSslBuild(WriteFile("cert.pem", certificate.ExportCertificatePem()), "pass phrase", keyFile, null, null);
        }

        ActLoad(loaded, failure);
        Diagnostics.Assert("failure is null", true, failure is null);
        Assert.IsNull(failure);
        using (loaded)
        {
            Diagnostics.Assert("signs with the scheme", true, HandBuiltTlsProvider.ToTlsClientCertificate(loaded)!.SigningKey.CanSign((ushort)scheme));
            Assert.IsTrue(HandBuiltTlsProvider.ToTlsClientCertificate(loaded)!.SigningKey.CanSign((ushort)scheme));
        }
    }

    [TestMethod]
    [DataRow("wrong")]
    [DataRow(null)]
    public void LoadAsOpenSslBuild_WithAnEncryptedPemKeyAndAWrongOrNoPassphrase_FailsAsAnUnusableKey(string? passphrase)
    {
        var (publicKey, privateKeyInfo) = KeyPair(HandBuiltPrivateKeyReader.Ed25519Oid);
        using var certificate = CertificateFor(HandBuiltPrivateKeyReader.Ed25519Oid, publicKey);
        var keyFile = WriteEncryptedKey(privateKeyInfo);
        Diagnostics.Arrange("key algorithm", "Ed25519");
        Diagnostics.Arrange("passphrase", passphrase ?? "none");

        X509Certificate2? loaded;
        ConnectResult? failure;
        using (Diagnostics.Phase("decrypt and load"))
        {
            (loaded, failure) = ClientCertificateLoader.LoadAsOpenSslBuild(WriteFile("cert.pem", certificate.ExportCertificatePem()), passphrase, keyFile, null, null);
        }

        ActLoad(loaded, failure);
        Diagnostics.Assert("loaded is null", true, loaded is null);
        Diagnostics.Assert("exit code", CurlExitCode.BadFunctionArgument, failure?.ExitCode);
        Diagnostics.Assert("error message", WithoutDirectory($"unable to set private key file: '{keyFile}' type PEM"), WithoutDirectory(failure?.ErrorMessage));
        Assert.IsNull(loaded);
        Assert.AreEqual(CurlExitCode.BadFunctionArgument, failure!.ExitCode);
        Assert.AreEqual($"unable to set private key file: '{keyFile}' type PEM", failure.ErrorMessage);
    }

    private string WriteEncryptedKey(byte[] privateKeyInfo)
    {
        var encrypted = EncryptedPrivateKeyInfoDecryptionTests.Encrypt(
            privateKeyInfo,
            "pass phrase",
            new EncryptedPrivateKeyInfoDecryptionTests.Encryption("2.16.840.1.101.3.4.1.42", 32, "1.2.840.113549.2.9"));
        return WriteFile("key.pem", PemEncoding.WriteString("ENCRYPTED PRIVATE KEY", encrypted));
    }

    private void AssertUnusable(X509Certificate2 certificate, byte[] privateKeyInfo)
    {
        var (loaded, failure) = Load(certificate, privateKeyInfo, asDer: true);

        Diagnostics.Assert("loaded is null", true, loaded is null);
        Diagnostics.Assert("exit code", CurlExitCode.BadFunctionArgument, failure?.ExitCode);
        Diagnostics.Assert("error message starts with", "unable to set private key file: ", WithoutDirectory(failure?.ErrorMessage));
        Assert.IsNull(loaded);
        Assert.AreEqual(CurlExitCode.BadFunctionArgument, failure!.ExitCode);
        StringAssert.StartsWith(failure.ErrorMessage, "unable to set private key file: ");
    }

    private (X509Certificate2? Certificate, ConnectResult? Failure) Load(X509Certificate2 certificate, byte[] privateKeyInfo, bool asDer)
    {
        var certificateFile = WriteFile("cert.pem", certificate.ExportCertificatePem());
        var keyFile = asDer
            ? WriteFile("key.der", privateKeyInfo)
            : WriteFile("key.pem", PemEncoding.WriteString("PRIVATE KEY", privateKeyInfo));
        Diagnostics.Arrange("certificate", $"{certificate.Subject} issued by {certificate.Issuer}");
        Diagnostics.Arrange("--key-type", asDer ? "DER" : "PEM");
        Diagnostics.Bytes("PrivateKeyInfo", privateKeyInfo);

        X509Certificate2? loaded;
        ConnectResult? failure;
        using (Diagnostics.Phase("load"))
        {
            (loaded, failure) = ClientCertificateLoader.LoadAsOpenSslBuild(certificateFile, null, keyFile, null, asDer ? "DER" : null);
        }

        ActLoad(loaded, failure);
        return (loaded, failure);
    }

    // The temporary directory differs by machine and platform, so diagnostics name it <temp>.
    private string? WithoutDirectory(string? text) => text?.Replace(_directory, "<temp>", StringComparison.Ordinal);

    private void ActLoad(X509Certificate2? loaded, ConnectResult? failure)
    {
        Diagnostics.Act("loaded", loaded is null ? "null" : $"{loaded.GetType().Name} {loaded.Subject}");
        Diagnostics.Act("failure", failure is null ? "null" : $"{failure.ExitCode}: {WithoutDirectory(failure.ErrorMessage)}");
    }

    private string WriteFile(string name, string contents) => WriteFile(name, System.Text.Encoding.ASCII.GetBytes(contents));

    private string WriteFile(string name, byte[] contents)
    {
        Directory.CreateDirectory(_directory);
        var path = Path.Combine(_directory, name);
        File.WriteAllBytes(path, contents);
        return path;
    }

    private static (byte[] PublicKey, byte[] PrivateKeyInfo) KeyPair(string keyAlgorithmOid)
    {
        switch (keyAlgorithmOid)
        {
            case HandBuiltPrivateKeyReader.Ed25519Oid:
                var ed25519 = new byte[Ed25519.PublicKeySize];
                Ed25519.ComputePublicKey(Seed32, ed25519);
                return (ed25519, PrivateKeyInfo(keyAlgorithmOid, OctetString(Seed32)));
            case HandBuiltPrivateKeyReader.Ed448Oid:
                var ed448 = new byte[Ed448.PublicKeySize];
                Ed448.ComputePublicKey(Seed57, ed448);
                return (ed448, PrivateKeyInfo(keyAlgorithmOid, OctetString(Seed57)));
            default:
                var parameterSet = keyAlgorithmOid switch
                {
                    HandBuiltPrivateKeyReader.MlDsa44Oid => MlDsaParameterSet.MlDsa44,
                    HandBuiltPrivateKeyReader.MlDsa65Oid => MlDsaParameterSet.MlDsa65,
                    _ => MlDsaParameterSet.MlDsa87,
                };
                using (var key = MlDsa.GenerateKey(parameterSet, Seed32))
                {
                    return (PublicKeyOf(key), PrivateKeyInfo(keyAlgorithmOid, MlDsaSeedForm(Seed32)));
                }
        }
    }

    private static X509Certificate2 CertificateFor(string keyAlgorithmOid, byte[] publicKey)
    {
        var (issuerKey, issuer) = Issuer.Value;
        var subjectPublicKeyInfo = Sequence(writer =>
        {
            using (writer.PushSequence())
            {
                writer.WriteObjectIdentifier(keyAlgorithmOid);
            }

            writer.WriteBitString(publicKey);
        });
        var subjectPublicKey = PublicKey.CreateFromSubjectPublicKeyInfo(subjectPublicKeyInfo, out _);
        var request = new CertificateRequest(new X500DistinguishedName("CN=client"), subjectPublicKey, HashAlgorithmName.SHA256);
        return request.Create(
            issuer.SubjectName,
            X509SignatureGenerator.CreateForRSA(issuerKey, RSASignaturePadding.Pkcs1),
            DateTimeOffset.UtcNow.AddDays(-1),
            DateTimeOffset.UtcNow.AddDays(1),
            [1, 2, 3, 4]);
    }

    private static (RSA Key, X509Certificate2 Certificate) CreateIssuer()
    {
        var key = RSA.Create(2048);
        var certificate = new CertificateRequest("CN=issuer", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1)
            .CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
        return (key, certificate);
    }

    private static byte[] PublicKeyOf(MlDsa key)
    {
        var publicKey = new byte[MlDsa.GetPublicKeySize(key.ParameterSet)];
        key.ExportPublicKey(publicKey);
        return publicKey;
    }

    private static byte[] ExpandedKeyOf(MlDsa key)
    {
        var privateKey = new byte[MlDsa.GetPrivateKeySize(key.ParameterSet)];
        key.ExportPrivateKey(privateKey);
        return privateKey;
    }

    private static byte[] PrivateKeyInfo(string keyAlgorithmOid, byte[] privateKey) => Sequence(writer =>
    {
        writer.WriteInteger(0);
        using (writer.PushSequence())
        {
            writer.WriteObjectIdentifier(keyAlgorithmOid);
        }

        writer.WriteOctetString(privateKey);
    });

    private static byte[] MlDsaSeedForm(byte[] seed)
    {
        var writer = new AsnWriter(AsnEncodingRules.DER);
        writer.WriteOctetString(seed, new Asn1Tag(TagClass.ContextSpecific, 0));
        return writer.Encode();
    }

    private static byte[] MlDsaBothForm(byte[] seed, byte[] expandedKey) => Sequence(writer =>
    {
        writer.WriteOctetString(seed);
        writer.WriteOctetString(expandedKey);
    });

    private static byte[] OctetString(byte[] contents)
    {
        var writer = new AsnWriter(AsnEncodingRules.DER);
        writer.WriteOctetString(contents);
        return writer.Encode();
    }

    private static byte[] Integer()
    {
        var writer = new AsnWriter(AsnEncodingRules.DER);
        writer.WriteInteger(0);
        return writer.Encode();
    }

    private static byte[] Sequence(Action<AsnWriter> writeContents)
    {
        var writer = new AsnWriter(AsnEncodingRules.DER);
        using (writer.PushSequence())
        {
            writeContents(writer);
        }

        return writer.Encode();
    }
}
