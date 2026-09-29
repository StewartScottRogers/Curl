using System.Text;
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

        Tls12HandshakeOutput output = Run(client, server);

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

        AssertCompletesWithServerKeys(client, server, Run(client, server));
    }

    [TestMethod]
    [DataRow(TlsProtocolVersion.Tls10)]
    [DataRow(TlsProtocolVersion.Tls11)]
    public void HandshakeCompletesBelowTlsOneTwo(TlsProtocolVersion version)
    {
        Tls12TestServer server = new(TestServerCredential.Rsa(TlsSignatureScheme.RsaPkcs1Sha256)) { Version = version, CipherSuite = 0xc021 };
        Tls12ClientHandshake client = Client(SrpSettings);

        AssertCompletesWithServerKeys(client, server, Run(client, server));
        Assert.AreEqual(version, client.Version);
    }

    [TestMethod]
    public void ClientHelloCarriesTheUserNameInSrpAfterServerName()
    {
        Tls12ClientHandshake client = Client(SrpSettings with { SrpCredentials = new TlsSrpCredentials("jürgen", "x") });

        ClientHello hello = ClientHello.Decode(Body(client.Start().MessagesToSend[0])).Value;

        List<TlsExtensionType> types = [.. hello.Extensions.Select(extension => extension.Type)];
        Assert.AreEqual(types.IndexOf(TlsExtensionType.ServerName) + 1, types.IndexOf(TlsExtensionType.Srp));
        CollectionAssert.AreEqual(Encoding.UTF8.GetBytes("jürgen"), SrpExtension.Decode(hello.Extensions.Single(extension => extension.Type == TlsExtensionType.Srp).Data).Value);
        CollectionAssert.AreEqual(SrpSuites, hello.CipherSuites.ToArray());
    }

    [TestMethod]
    public void WithoutCredentialsTheHelloOffersNoSrpSuiteAndNoSrpExtension()
    {
        Tls12ClientHandshake client = Client(SrpSettings with { SrpCredentials = null, CipherSuites = [0xc01d, 0xc02b, 0xc021] });

        ClientHello hello = ClientHello.Decode(Body(client.Start().MessagesToSend[0])).Value;

        CollectionAssert.AreEqual(new ushort[] { 0xc02b }, hello.CipherSuites.ToArray());
        Assert.IsFalse(hello.Extensions.Any(extension => extension.Type == TlsExtensionType.Srp));
    }

    [TestMethod]
    public void OnlySrpSuitesWithoutCredentialsCannotDriveAHandshake() =>
        Assert.ThrowsExactly<ArgumentException>(() => Client(SrpSettings with { SrpCredentials = null }));

    [TestMethod]
    public void AServerChoosingAnSrpSuiteTheHelloLeftOutIsIllegalParameter()
    {
        Tls12ClientHandshake client = Client(SrpSettings with { SrpCredentials = null, CipherSuites = [0xc01d, 0x00a6] });
        client.Start();
        byte[] serverHello = new ServerHello((ushort)TlsProtocolVersion.Tls12, new byte[32], [], 0xc01d, 0, [RenegotiationInfoExtension.Encode([])]).Encode();

        AssertFails(TlsAlertDescription.IllegalParameter, client.ReceiveHandshake(serverHello));
    }

    [TestMethod]
    public void AWrongPasswordFailsTheServersFinishedWithDecryptError()
    {
        Tls12TestServer server = new(null) { CipherSuite = 0xc01d, CheckClientFinished = false };
        Tls12ClientHandshake client = Client(SrpSettings with { SrpCredentials = new TlsSrpCredentials("alice", "password124") });

        AssertFails(TlsAlertDescription.DecryptError, Run(client, server));
    }

    [TestMethod]
    public void AGroupOutsideAppendixAIsInsufficientSecurity()
    {
        byte[] prime = Curl.Cryptography.FiniteFieldDiffieHellmanGroup.Ffdhe2048.Prime.ToArray();

        AssertServerKeyExchangeFails(TlsAlertDescription.InsufficientSecurity, new Tls12SrpParameters(prime, [2], [1, 2, 3], [5]));
    }

    [TestMethod]
    public void AKnownPrimeWithTheWrongGeneratorIsInsufficientSecurity() =>
        AssertServerKeyExchangeFails(TlsAlertDescription.InsufficientSecurity, new Tls12SrpParameters(SrpGroup.Bits1024.Prime.ToArray(), [5], [1, 2, 3], [5]));

    [TestMethod]
    public void AServerValueOfZeroIsIllegalParameter() =>
        AssertServerKeyExchangeFails(TlsAlertDescription.IllegalParameter, new Tls12SrpParameters(SrpGroup.Bits1024.Prime.ToArray(), [2], [1, 2, 3], [0]));

    [TestMethod]
    public void AServerValueOfNIsIllegalParameter() =>
        AssertServerKeyExchangeFails(TlsAlertDescription.IllegalParameter, new Tls12SrpParameters(SrpGroup.Bits1024.Prime.ToArray(), [2], [1, 2, 3], SrpGroup.Bits1024.Prime.ToArray()));

    [TestMethod]
    public void AServerValueAboveNIsIllegalParameter() =>
        AssertServerKeyExchangeFails(TlsAlertDescription.IllegalParameter, new Tls12SrpParameters(SrpGroup.Bits1024.Prime.ToArray(), [2], [1, 2, 3], [1, .. SrpGroup.Bits1024.Prime]));

    [TestMethod]
    public void ATruncatedSrpServerKeyExchangeIsDecodeError()
    {
        byte[] encoded = new Tls12ServerKeyExchange(new Tls12SrpParameters(SrpGroup.Bits1024.Prime.ToArray(), [2], [1, 2, 3], [5]), null, null).Encode();
        byte[] truncated = new HandshakeMessage(HandshakeType.ServerKeyExchange, encoded[4..^1]).Encode();
        Tls12TestServer server = new(null) { CipherSuite = 0xc01d };

        AssertFails(TlsAlertDescription.DecodeError, Run(Client(SrpSettings), server, flight => Replace(flight, HandshakeType.ServerKeyExchange, truncated)));
    }

    [TestMethod]
    public void SrpParametersRoundTrip()
    {
        Tls12SrpParameters parameters = new(SrpGroup.Bits1024.Prime.ToArray(), [2], [9, 8, 7], [1, 2]);
        byte[] encoded = new Tls12ServerKeyExchange(parameters, null, null).Encode();

        Tls12SrpParameters decoded = (Tls12SrpParameters)Tls12ServerKeyExchange.Decode(encoded[4..], Tls12KeyExchange.Srp, signed: false, hasSignatureAlgorithm: true).Value.Parameters;

        CollectionAssert.AreEqual(parameters.Prime, decoded.Prime);
        CollectionAssert.AreEqual(parameters.Generator, decoded.Generator);
        CollectionAssert.AreEqual(parameters.Salt, decoded.Salt);
        CollectionAssert.AreEqual(parameters.PublicValue, decoded.PublicValue);
    }

    [TestMethod]
    public void CombinedHelloCarriesSrpAndTheSrpSuitesOnlyWithCredentials()
    {
        Tls12ClientSettings lower = SrpSettings with { MinimumVersion = TlsProtocolVersion.Tls12, CipherSuites = [0xc02b, 0xc01d] };

        ClientHello with = CombinedHello(lower);
        ClientHello without = CombinedHello(lower with { SrpCredentials = null });

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

    private static void AssertServerKeyExchangeFails(TlsAlertDescription alert, Tls12SrpParameters parameters)
    {
        byte[] replacement = new Tls12ServerKeyExchange(parameters, null, null).Encode();
        Tls12TestServer server = new(null) { CipherSuite = 0xc01d };

        AssertFails(alert, Run(Client(SrpSettings), server, flight => Replace(flight, HandshakeType.ServerKeyExchange, replacement)));
    }

    private static TestServerCredential? Credential(string name) => name switch
    {
        "rsa" => TestServerCredential.Rsa(TlsSignatureScheme.RsaPkcs1Sha256),
        "dss" => TestServerCredential.Dsa(TlsSignatureScheme.DsaSha256),
        _ => null,
    };

    private static void AssertCompletesWithServerKeys(Tls12ClientHandshake client, Tls12TestServer server, Tls12HandshakeOutput output)
    {
        Assert.IsNull(output.Failure);
        Assert.IsTrue(output.IsComplete);
        CollectionAssert.AreEqual(server.MasterSecret, client.Session!.MasterSecret);
        using Tls12RecordWriteState write = Tls12RecordWriteState.Create(client.RecordProtection!, client.KeyBlock!.ClientWrite, SystemTlsRandomSource.Instance, insertEmptyFragment: false);
        using Tls12RecordReadState read = Tls12RecordReadState.Create(client.RecordProtection!, server.KeyBlock.ClientWrite);
        byte[] record = write.Protect(TlsContentType.ApplicationData, "GET / HTTP/1.1"u8);
        CollectionAssert.AreEqual("GET / HTTP/1.1"u8.ToArray(), read.Unprotect(TlsContentType.ApplicationData, record.AsSpan(5)).Value);
    }
}
