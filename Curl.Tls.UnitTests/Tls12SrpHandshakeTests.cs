using System.Text;
using Curl.Testing;
using static Curl.Tls.Tls12HandshakeDriver;

namespace Curl.Tls;

/// <summary>
/// Runs the TLS-SRP key exchange (RFC 5054) against the in-memory server: every SRP suite,
/// several Appendix A groups, TLS 1.0 as well as 1.2, the <c>srp</c> extension in the
/// ClientHello, and the typed alerts for a wrong password, a group outside Appendix A and a
/// B that is 0 modulo N.
/// </summary>
[TestClass]
public sealed class Tls12SrpHandshakeTests
{
    private static readonly ushort[] SrpSuites = [0xc020, 0xc01d, 0xc01a, 0xc021, 0xc01e, 0xc01b, 0xc022, 0xc01f, 0xc01c];

    private static readonly Tls12ClientSettings SrpSettings = DefaultSettings with
    {
        MinimumVersion = TlsProtocolVersion.Tls10,
        CipherSuites = SrpSuites,
        SrpCredentials = new TlsSrpCredentials("alice", "password123"),
        SignatureAlgorithms = [TlsSignatureScheme.RsaPkcs1Sha256, TlsSignatureScheme.DsaSha256],
    };

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    [DataRow((ushort)0xc020, "none", DisplayName = "TLS_SRP_SHA_WITH_AES_256_CBC_SHA")]
    [DataRow((ushort)0xc01d, "none", DisplayName = "TLS_SRP_SHA_WITH_AES_128_CBC_SHA")]
    [DataRow((ushort)0xc01a, "none", DisplayName = "TLS_SRP_SHA_WITH_3DES_EDE_CBC_SHA")]
    [DataRow((ushort)0xc021, "rsa", DisplayName = "TLS_SRP_SHA_RSA_WITH_AES_256_CBC_SHA")]
    [DataRow((ushort)0xc01e, "rsa", DisplayName = "TLS_SRP_SHA_RSA_WITH_AES_128_CBC_SHA")]
    [DataRow((ushort)0xc01b, "rsa", DisplayName = "TLS_SRP_SHA_RSA_WITH_3DES_EDE_CBC_SHA")]
    [DataRow((ushort)0xc022, "dss", DisplayName = "TLS_SRP_SHA_DSS_WITH_AES_256_CBC_SHA")]
    [DataRow((ushort)0xc01f, "dss", DisplayName = "TLS_SRP_SHA_DSS_WITH_AES_128_CBC_SHA")]
    [DataRow((ushort)0xc01c, "dss", DisplayName = "TLS_SRP_SHA_DSS_WITH_3DES_EDE_CBC_SHA")]
    public void HandshakeCompletesForEachSrpSuiteAndItsKeysProtectRecords(int cipherSuite, string credential)
    {
        RecordingCertificateVerifier verifier = new();
        Tls12TestServer server = new(Credential(credential)) { CipherSuite = (ushort)cipherSuite };
        Tls12ClientHandshake client = Client(SrpSettings, verifier);
        Diagnostics.Arrange("server", $"cipher suite 0x{cipherSuite:x4}, credential {credential}");

        Tls12HandshakeOutput output = Run(client, server, diagnostics: Diagnostics);
        Diagnostics.Act("handshake", $"{Describe(output)}; suite 0x{client.CipherSuite?.Code:x4}, key exchange {client.CipherSuite?.KeyExchange}, certificates presented {verifier.Presented.Count}");

        Diagnostics.Assert("cipher suite", cipherSuite, client.CipherSuite!.Code);
        AssertCompletesWithServerKeys(client, server, output);
        Assert.AreEqual(cipherSuite, client.CipherSuite!.Code);
        Assert.AreEqual(Tls12KeyExchange.Srp, client.CipherSuite.KeyExchange);
        Assert.HasCount(credential == "none" ? 0 : 1, verifier.Presented);
        Assert.IsNull(client.NegotiatedGroup);
    }

    [TestMethod]
    [DataRow(1)]
    [DataRow(2)]
    [DataRow(3)]
    [DataRow(6)]
    public void HandshakeCompletesInOtherAppendixAGroups(int groupIndex)
    {
        Tls12TestServer server = new(null) { CipherSuite = 0xc01d, SrpGroup = SrpGroup.All[groupIndex] };
        Tls12ClientHandshake client = Client(SrpSettings);
        Diagnostics.Arrange("server", $"suite 0xc01d, Appendix A group {groupIndex} of {SrpGroup.All[groupIndex].Prime.Length * 8} bits");

        Tls12HandshakeOutput output = Run(client, server, diagnostics: Diagnostics);
        Diagnostics.Act("handshake", Describe(output));

        AssertCompletesWithServerKeys(client, server, output);
    }

    [TestMethod]
    [DataRow(TlsProtocolVersion.Tls10)]
    [DataRow(TlsProtocolVersion.Tls11)]
    public void HandshakeCompletesBelowTlsOneTwo(TlsProtocolVersion version)
    {
        Tls12TestServer server = new(TestServerCredential.Rsa(TlsSignatureScheme.RsaPkcs1Sha256)) { Version = version, CipherSuite = 0xc021 };
        Tls12ClientHandshake client = Client(SrpSettings);
        Diagnostics.Arrange("server", $"version {version}, suite 0xc021 with an RSA certificate");

        Tls12HandshakeOutput output = Run(client, server, diagnostics: Diagnostics);
        Diagnostics.Act("handshake", $"{Describe(output)}; version {client.Version}");

        AssertCompletesWithServerKeys(client, server, output);
        Diagnostics.Assert("version", version, client.Version);
        Assert.AreEqual(version, client.Version);
    }

    [TestMethod]
    public void ClientHelloCarriesTheUserNameInSrpAfterServerName()
    {
        Tls12ClientHandshake client = Client(SrpSettings with { SrpCredentials = new TlsSrpCredentials("jürgen", "x") });
        Diagnostics.Arrange("user name", "jürgen");

        ClientHello hello = ClientHello.Decode(Body(client.Start().MessagesToSend[0])).Value;

        List<TlsExtensionType> types = [.. hello.Extensions.Select(extension => extension.Type)];
        byte[] userName = SrpExtension.Decode(hello.Extensions.Single(extension => extension.Type == TlsExtensionType.Srp).Data).Value;
        Diagnostics.Act("extensions", string.Join(", ", types));
        Diagnostics.Act("cipher suites", string.Join(", ", hello.CipherSuites.ToArray().Select(suite => $"0x{suite:x4}")));
        Diagnostics.Assert("srp position", types.IndexOf(TlsExtensionType.ServerName) + 1, types.IndexOf(TlsExtensionType.Srp));
        Diagnostics.Diff("srp user name", Encoding.UTF8.GetBytes("jürgen"), userName);
        Assert.AreEqual(types.IndexOf(TlsExtensionType.ServerName) + 1, types.IndexOf(TlsExtensionType.Srp));
        CollectionAssert.AreEqual(Encoding.UTF8.GetBytes("jürgen"), userName);
        CollectionAssert.AreEqual(SrpSuites, hello.CipherSuites.ToArray());
    }

    [TestMethod]
    public void WithoutCredentialsTheHelloOffersNoSrpSuiteAndNoSrpExtension()
    {
        Tls12ClientHandshake client = Client(SrpSettings with { SrpCredentials = null, CipherSuites = [0xc01d, 0xc02b, 0xc021] });
        Diagnostics.Arrange("settings", "no SRP credentials; suites 0xc01d, 0xc02b, 0xc021");

        ClientHello hello = ClientHello.Decode(Body(client.Start().MessagesToSend[0])).Value;
        Diagnostics.Act("cipher suites", string.Join(", ", hello.CipherSuites.ToArray().Select(suite => $"0x{suite:x4}")));

        Diagnostics.Assert("srp extension offered", false, hello.Extensions.Any(extension => extension.Type == TlsExtensionType.Srp));
        CollectionAssert.AreEqual(new ushort[] { 0xc02b }, hello.CipherSuites.ToArray());
        Assert.IsFalse(hello.Extensions.Any(extension => extension.Type == TlsExtensionType.Srp));
    }

    [TestMethod]
    public void OnlySrpSuitesWithoutCredentialsCannotDriveAHandshake()
    {
        Diagnostics.Arrange("settings", "only SRP suites, no SRP credentials");

        ArgumentException failure = Assert.ThrowsExactly<ArgumentException>(() => Client(SrpSettings with { SrpCredentials = null }));
        Diagnostics.Act("failure", failure.GetType().Name);

        Diagnostics.Assert("exception", nameof(ArgumentException), failure.GetType().Name);
    }

    [TestMethod]
    public void AServerChoosingAnSrpSuiteTheHelloLeftOutIsIllegalParameter()
    {
        Tls12ClientHandshake client = Client(SrpSettings with { SrpCredentials = null, CipherSuites = [0xc01d, 0x00a6] });
        client.Start();
        byte[] serverHello = new ServerHello((ushort)TlsProtocolVersion.Tls12, new byte[32], [], 0xc01d, 0, [RenegotiationInfoExtension.Encode([])]).Encode();
        Diagnostics.Arrange("server hello", "chooses 0xc01d, which the hello left out for want of credentials");

        Tls12HandshakeOutput output = client.ReceiveHandshake(serverHello);
        Diagnostics.Act("handshake", Describe(output));

        Diagnostics.Assert("alert", TlsAlertDescription.IllegalParameter, output.Failure?.Alert);
        AssertFails(TlsAlertDescription.IllegalParameter, output);
    }

    [TestMethod]
    public void AWrongPasswordFailsTheServersFinishedWithDecryptError()
    {
        Tls12TestServer server = new(null) { CipherSuite = 0xc01d, CheckClientFinished = false };
        Tls12ClientHandshake client = Client(SrpSettings with { SrpCredentials = new TlsSrpCredentials("alice", "password124") });
        Diagnostics.Arrange("password", "client password124, server password123");

        Tls12HandshakeOutput output = Run(client, server, diagnostics: Diagnostics);
        Diagnostics.Act("handshake", Describe(output));

        Diagnostics.Assert("alert", TlsAlertDescription.DecryptError, output.Failure?.Alert);
        AssertFails(TlsAlertDescription.DecryptError, output);
    }

    [TestMethod]
    public void AGroupOutsideAppendixAIsInsufficientSecurity()
    {
        byte[] prime = Curl.Cryptography.FiniteFieldDiffieHellmanGroup.Ffdhe2048.Prime.ToArray();

        AssertServerKeyExchangeFails(TlsAlertDescription.InsufficientSecurity, "the ffdhe2048 prime with generator 2", new Tls12SrpParameters(prime, [2], [1, 2, 3], [5]));
    }

    [TestMethod]
    public void AKnownPrimeWithTheWrongGeneratorIsInsufficientSecurity() =>
        AssertServerKeyExchangeFails(TlsAlertDescription.InsufficientSecurity, "the 1024-bit prime with generator 5", new Tls12SrpParameters(SrpGroup.Bits1024.Prime.ToArray(), [5], [1, 2, 3], [5]));

    [TestMethod]
    public void AServerValueOfZeroIsIllegalParameter() =>
        AssertServerKeyExchangeFails(TlsAlertDescription.IllegalParameter, "B of 0", new Tls12SrpParameters(SrpGroup.Bits1024.Prime.ToArray(), [2], [1, 2, 3], [0]));

    [TestMethod]
    public void AServerValueOfNIsIllegalParameter() =>
        AssertServerKeyExchangeFails(TlsAlertDescription.IllegalParameter, "B equal to N", new Tls12SrpParameters(SrpGroup.Bits1024.Prime.ToArray(), [2], [1, 2, 3], SrpGroup.Bits1024.Prime.ToArray()));

    [TestMethod]
    public void AServerValueAboveNIsIllegalParameter() =>
        AssertServerKeyExchangeFails(TlsAlertDescription.IllegalParameter, "B one byte longer than N", new Tls12SrpParameters(SrpGroup.Bits1024.Prime.ToArray(), [2], [1, 2, 3], [1, .. SrpGroup.Bits1024.Prime]));

    [TestMethod]
    public void ATruncatedSrpServerKeyExchangeIsDecodeError()
    {
        byte[] encoded = new Tls12ServerKeyExchange(new Tls12SrpParameters(SrpGroup.Bits1024.Prime.ToArray(), [2], [1, 2, 3], [5]), null, null).Encode();
        byte[] truncated = new HandshakeMessage(HandshakeType.ServerKeyExchange, encoded[4..^1]).Encode();
        Tls12TestServer server = new(null) { CipherSuite = 0xc01d };
        Diagnostics.Arrange("server key exchange", $"body cut from {encoded.Length - 4} to {encoded.Length - 5} bytes");

        Tls12HandshakeOutput output = Run(Client(SrpSettings), server, flight => Replace(flight, HandshakeType.ServerKeyExchange, truncated), diagnostics: Diagnostics);
        Diagnostics.Act("handshake", Describe(output));

        Diagnostics.Assert("alert", TlsAlertDescription.DecodeError, output.Failure?.Alert);
        AssertFails(TlsAlertDescription.DecodeError, output);
    }

    [TestMethod]
    public void SrpParametersRoundTrip()
    {
        Tls12SrpParameters parameters = new(SrpGroup.Bits1024.Prime.ToArray(), [2], [9, 8, 7], [1, 2]);
        byte[] encoded = new Tls12ServerKeyExchange(parameters, null, null).Encode();
        Diagnostics.Arrange("parameters", "1024-bit prime, generator 2, salt 090807, B 0102");
        Diagnostics.Bytes("encoded", encoded);

        Tls12SrpParameters decoded = (Tls12SrpParameters)Tls12ServerKeyExchange.Decode(encoded[4..], Tls12KeyExchange.Srp, signed: false, hasSignatureAlgorithm: true).Value.Parameters;
        Diagnostics.Act("decoded", $"prime {decoded.Prime.Length} bytes, generator {Convert.ToHexString(decoded.Generator)}, salt {Convert.ToHexString(decoded.Salt)}, B {Convert.ToHexString(decoded.PublicValue)}");

        Diagnostics.Diff("prime", parameters.Prime, decoded.Prime);
        Diagnostics.Diff("salt", parameters.Salt, decoded.Salt);
        CollectionAssert.AreEqual(parameters.Prime, decoded.Prime);
        CollectionAssert.AreEqual(parameters.Generator, decoded.Generator);
        CollectionAssert.AreEqual(parameters.Salt, decoded.Salt);
        CollectionAssert.AreEqual(parameters.PublicValue, decoded.PublicValue);
    }

    [TestMethod]
    public void CombinedHelloCarriesSrpAndTheSrpSuitesOnlyWithCredentials()
    {
        Tls12ClientSettings lower = SrpSettings with { MinimumVersion = TlsProtocolVersion.Tls12, CipherSuites = [0xc02b, 0xc01d] };
        Diagnostics.Arrange("lower versions", "TLS 1.2 with suites 0xc02b, 0xc01d, with and without SRP credentials");

        ClientHello with = CombinedHello(lower);
        ClientHello without = CombinedHello(lower with { SrpCredentials = null });
        Diagnostics.Act("with credentials", $"offers 0xc01d {with.CipherSuites.Contains((ushort)0xc01d)}, srp extension {with.Extensions.Any(extension => extension.Type == TlsExtensionType.Srp)}");
        Diagnostics.Act("without credentials", $"offers 0xc01d {without.CipherSuites.Contains((ushort)0xc01d)}, srp extension {without.Extensions.Any(extension => extension.Type == TlsExtensionType.Srp)}");

        Diagnostics.Assert("srp suite offered with credentials", true, with.CipherSuites.Contains((ushort)0xc01d));
        Assert.IsTrue(with.CipherSuites.Contains((ushort)0xc01d));
        Assert.IsTrue(with.Extensions.Any(extension => extension.Type == TlsExtensionType.Srp));
        Assert.IsFalse(without.CipherSuites.Contains((ushort)0xc01d));
        Assert.IsFalse(without.Extensions.Any(extension => extension.Type == TlsExtensionType.Srp));
    }

    private static ClientHello CombinedHello(Tls12ClientSettings lower)
    {
        Tls13ClientHandshake client = HandshakeDriver.Client(HandshakeDriver.DefaultSettings with { LowerVersions = lower });
        byte[] hello = client.Start().BytesToSend[0].Bytes;
        return ClientHello.Decode(HandshakeMessageReader.Read(hello).Message!.Body).Value;
    }

    private static string Describe(Tls12HandshakeOutput output) =>
        $"complete {output.IsComplete}, failure {output.Failure?.Alert.ToString() ?? "none"}";

    private void AssertServerKeyExchangeFails(TlsAlertDescription alert, string description, Tls12SrpParameters parameters)
    {
        byte[] replacement = new Tls12ServerKeyExchange(parameters, null, null).Encode();
        Tls12TestServer server = new(null) { CipherSuite = 0xc01d };
        Diagnostics.Arrange("server key exchange", description);

        Tls12HandshakeOutput output = Run(Client(SrpSettings), server, flight => Replace(flight, HandshakeType.ServerKeyExchange, replacement), diagnostics: Diagnostics);
        Diagnostics.Act("handshake", Describe(output));

        Diagnostics.Assert("alert", alert, output.Failure?.Alert);
        AssertFails(alert, output);
    }

    private static TestServerCredential? Credential(string name) => name switch
    {
        "rsa" => TestServerCredential.Rsa(TlsSignatureScheme.RsaPkcs1Sha256),
        "dss" => TestServerCredential.Dsa(TlsSignatureScheme.DsaSha256),
        _ => null,
    };

    private void AssertCompletesWithServerKeys(Tls12ClientHandshake client, Tls12TestServer server, Tls12HandshakeOutput output)
    {
        Diagnostics.Assert("complete", true, output.IsComplete);
        Assert.IsNull(output.Failure);
        Assert.IsTrue(output.IsComplete);
        CollectionAssert.AreEqual(server.MasterSecret, client.Session!.MasterSecret);
        using Tls12RecordWriteState write = Tls12RecordWriteState.Create(client.RecordProtection!, client.KeyBlock!.ClientWrite, SystemTlsRandomSource.Instance, insertEmptyFragment: false);
        using Tls12RecordReadState read = Tls12RecordReadState.Create(client.RecordProtection!, server.KeyBlock.ClientWrite);
        byte[] record = write.Protect(TlsContentType.ApplicationData, "GET / HTTP/1.1"u8);
        byte[] unprotected = read.Unprotect(TlsContentType.ApplicationData, record.AsSpan(5)).Value;
        Diagnostics.Diff("record round trip", "GET / HTTP/1.1"u8, unprotected);
        CollectionAssert.AreEqual("GET / HTTP/1.1"u8.ToArray(), unprotected);
    }
}
