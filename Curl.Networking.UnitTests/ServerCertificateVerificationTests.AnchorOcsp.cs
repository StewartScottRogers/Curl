using System.Formats.Asn1;
using System.Net.Security;
using System.Numerics;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Curl.Networking.Fakes.Tls13Server;
using Curl.Protocol.Abstractions;
using Curl.Tls;
using X509CertificateRequest = System.Security.Cryptography.X509Certificates.CertificateRequest;

namespace Curl.Networking;

/// <summary>
/// Pins the OCSP check below a non-self-signed <c>--cacert</c> or <c>--proxy-cacert</c> anchor,
/// as curl 8.22.0 Schannel answers it (STD-196, BL-5877): OCSP revoked is revoked whatever the
/// CRL says, OCSP good is accepted whatever the CRL says, and the CRL answers when OCSP cannot.
/// </summary>
[TestClass]
public sealed class ServerCertificateVerificationAnchorOcspTests
{
    private const string Host = "localhost";
    private const string Responder = "http://127.0.0.1:1/ocsp";
    private const string SecondResponder = "http://127.0.0.1:1/second";
    private const string CrlAddress = "http://127.0.0.1:1/int.crl";

    private string _directory = null!;

    public enum List
    {
        None,
        Clean,
        Revoking,
    }

    public enum Answer
    {
        Unfetchable,
        Unknown,
        BadlySigned,
        OutOfDate,
    }

    [TestInitialize]
    public void CreateDirectory()
    {
        _directory = Path.Combine(Path.GetTempPath(), "curl-anchor-ocsp-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_directory);
    }

    [TestCleanup]
    public void DeleteDirectory() => Directory.Delete(_directory, recursive: true);

    [TestMethod]
    [DataRow(List.None, false, false)]
    [DataRow(List.None, true, false)]
    [DataRow(List.Clean, false, false)]
    [DataRow(List.Clean, false, true)]
    public void VerifyPeer_OcspSaysRevoked_IsExit60WithTheRevokedText(List list, bool bestEffort, bool isProxy)
    {
        using var pki = new Pki(list, Responder);

        var failure = Verify(pki, _ => pki.Response(OcspStapleStatus.Revoked), bestEffort, isProxy);

        Assert.AreEqual(CurlExitCode.PeerFailedVerification, failure?.ExitCode);
        Assert.AreEqual(TlsFailureMessages.SchannelChainRevoked, failure?.Message);
    }

    [TestMethod]
    [DataRow(List.None)]
    [DataRow(List.Revoking)]
    public void VerifyPeer_OcspSaysGood_AcceptsWithoutAskingTheList(List list)
    {
        using var pki = new Pki(list, Responder);
        var fetchedLists = new List<Uri>();

        var failure = Verify(pki, _ => pki.Response(OcspStapleStatus.Good), fetchedLists: fetchedLists);

        Assert.IsNull(failure);
        Assert.IsEmpty(fetchedLists);
    }

    [TestMethod]
    [DataRow(Answer.Unfetchable)]
    [DataRow(Answer.Unknown)]
    [DataRow(Answer.BadlySigned)]
    [DataRow(Answer.OutOfDate)]
    public void VerifyPeer_NoUsableOcspAnswer_LeavesItToTheList(Answer answer)
    {
        using var clean = new Pki(List.Clean, Responder);
        using var revoking = new Pki(List.Revoking, Responder);
        using var listless = new Pki(List.None, Responder);

        Assert.IsNull(Verify(clean, _ => Unusable(clean, answer)));
        Assert.AreEqual(TlsFailureMessages.SchannelChainRevoked, Verify(revoking, _ => Unusable(revoking, answer))?.Message);
        Assert.AreEqual(TlsFailureMessages.SchannelRevocationStatusUnknown, Verify(listless, _ => Unusable(listless, answer))?.Message);
    }

    [TestMethod]
    public void VerifyPeer_OcspGoodFromAnExpiredDelegatedResponder_LeavesItToTheList()
    {
        using var revoking = new Pki(List.Revoking, Responder);
        using var listless = new Pki(List.None, Responder);

        var revoked = Verify(revoking, _ => revoking.DelegatedGoodResponse(TimeSpan.FromDays(-2)));
        var unknown = Verify(listless, _ => listless.DelegatedGoodResponse(TimeSpan.FromDays(-2)));

        Assert.AreEqual(CurlExitCode.PeerFailedVerification, revoked?.ExitCode);
        Assert.AreEqual(TlsFailureMessages.SchannelChainRevoked, revoked?.Message);
        Assert.AreEqual(CurlExitCode.PeerFailedVerification, unknown?.ExitCode);
        Assert.AreEqual(TlsFailureMessages.SchannelRevocationStatusUnknown, unknown?.Message);
    }

    [TestMethod]
    [DataRow(OcspStapleStatus.Good, false)]
    [DataRow(OcspStapleStatus.Revoked, false)]
    [DataRow(OcspStapleStatus.Revoked, true)]
    public void VerifyPeer_OcspAnswerUnderASha256CertId_IsUnknownUnlessBestEffort(OcspStapleStatus status, bool bestEffort)
    {
        using var pki = new Pki(List.None, Responder);

        var failure = Verify(
            pki,
            _ => (pki.ResponseBuilder(status) with { CertIdHashOid = OcspResponseBuilder.Sha256Oid }).Build(),
            bestEffort);

        Assert.AreEqual(bestEffort ? null : TlsFailureMessages.SchannelRevocationStatusUnknown, failure?.Message);
        Assert.AreEqual(bestEffort ? null : CurlExitCode.PeerFailedVerification, failure?.ExitCode);
    }

    [TestMethod]
    public void VerifyPeer_OcspGoodFromAValidDelegatedResponder_Accepts()
    {
        using var pki = new Pki(List.Revoking, Responder);

        var failure = Verify(pki, _ => pki.DelegatedGoodResponse(TimeSpan.FromDays(5)));

        Assert.IsNull(failure);
    }

    [TestMethod]
    public void VerifyPeer_OcspGoodFromANotYetValidDelegatedResponder_Accepts()
    {
        using var pki = new Pki(List.Revoking, Responder);

        var failure = Verify(pki, _ => pki.DelegatedGoodResponse(TimeSpan.FromDays(5), TimeSpan.FromDays(2)));

        Assert.IsNull(failure);
    }

    [TestMethod]
    public void VerifyPeer_FirstResponderUnfetchableAndSecondRevokes_IsRevoked()
    {
        using var pki = new Pki(List.Clean, Responder, SecondResponder);

        var failure = Verify(pki, uri => uri.AbsoluteUri.StartsWith(SecondResponder, StringComparison.Ordinal)
            ? pki.Response(OcspStapleStatus.Revoked)
            : throw new IOException("the server answered 404"));

        Assert.AreEqual(TlsFailureMessages.SchannelChainRevoked, failure?.Message);
    }

    [TestMethod]
    public void VerifyPeer_UnfetchableOcspResponse_IsWrittenToTheTlsLogAtWarning()
    {
        using var pki = new Pki(List.Clean, Responder);
        var log = new Fakes.RecordingDiagnosticLog(DiagnosticLogLevel.Warning);
        var asked = new List<Uri>();

        Verify(pki, uri => { asked.Add(uri); throw new IOException("the server answered 404"); }, log: log);

        var lines = log.At(DiagnosticLogLevel.Warning, DiagnosticLogComponents.Tls);
        Assert.AreEqual($"OCSP response {asked[0]} could not be fetched: the server answered 404", lines[0]);
    }

    [TestMethod]
    public void OcspResponderUris_HttpAndOtherAddresses_ListsOnlyTheHttpOnes()
    {
        using var pki = new Pki(List.None, "ldap://127.0.0.1/ocsp", Responder);

        var uris = ServerCertificateVerification.OcspResponderUris(pki.Leaf);

        CollectionAssert.AreEqual(new[] { new Uri(Responder) }, uris.ToArray());
    }

    [TestMethod]
    public void OcspResponderUris_NoExtensionOrOneThatDoesNotParse_ListsNone()
    {
        using var pki = new Pki(List.None, Responder);
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var request = new X509CertificateRequest("CN=Broken", key, HashAlgorithmName.SHA256);
        request.CertificateExtensions.Add(new X509Extension(new Oid("1.3.6.1.5.5.7.1.1"), [1, 2, 3], false));
        using var broken = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));

        Assert.IsEmpty(ServerCertificateVerification.OcspResponderUris(pki.Intermediate));
        Assert.IsEmpty(ServerCertificateVerification.OcspResponderUris(broken));
    }

    [TestMethod]
    [DataRow("http://127.0.0.1:1/ocsp")]
    [DataRow("http://127.0.0.1:1/ocsp/")]
    public void OcspGetAddress_ResponderWithOrWithoutASlash_AppendsTheEscapedBase64Request(string responder)
    {
        var address = ServerCertificateVerification.OcspGetAddress(new Uri(responder), [0xfb, 0xef, 0xff, 0x00]);

        Assert.AreEqual("http://127.0.0.1:1/ocsp/%2B%2B%2F%2FAA%3D%3D", address.AbsoluteUri);
    }

    [TestMethod]
    public void OcspRequestFor_ALeaf_NamesItByTheSha1CertificateIdentifier()
    {
        using var pki = new Pki(List.None, Responder);

        var request = ServerCertificateVerification.OcspRequestFor(pki.Leaf, pki.Intermediate);

        var certId = new AsnReader(request, AsnEncodingRules.DER).ReadSequence().ReadSequence().ReadSequence().ReadSequence().ReadSequence();
        Assert.AreEqual("1.3.14.3.2.26", certId.ReadSequence().ReadObjectIdentifier());
        CollectionAssert.AreEqual(SHA1.HashData(pki.Intermediate.SubjectName.RawData), certId.ReadOctetString());
        CollectionAssert.AreEqual(SHA1.HashData(pki.Intermediate.PublicKey.EncodedKeyValue.RawData), certId.ReadOctetString());
        CollectionAssert.AreEqual(pki.Leaf.SerialNumberBytes.ToArray(), certId.ReadIntegerBytes().ToArray());
        Assert.IsFalse(certId.HasData);
    }

    private static byte[] Unusable(Pki pki, Answer answer) => answer switch
    {
        Answer.Unknown => pki.Response(OcspStapleStatus.Unknown),
        Answer.BadlySigned => (pki.ResponseBuilder(OcspStapleStatus.Revoked) with { CorruptSignature = true }).Build(),
        Answer.OutOfDate => (pki.ResponseBuilder(OcspStapleStatus.Good) with
        {
            ThisUpdateOffset = TimeSpan.FromDays(-2),
            NextUpdateOffset = TimeSpan.FromDays(-1),
        }).Build(),
        _ => throw new IOException("no response"),
    };

    private (CurlExitCode ExitCode, string Message)? Verify(
        Pki pki, Func<Uri, byte[]> ocsp, bool bestEffort = false, bool isProxy = false, List<Uri>? fetchedLists = null, IDiagnosticLog? log = null)
    {
        var caFile = Path.Combine(_directory, "intermediate.pem");
        File.WriteAllText(caFile, pki.Intermediate.ExportCertificatePem());
        X509ChainStatus[] partial = [new() { Status = X509ChainStatusFlags.PartialChain | X509ChainStatusFlags.RevocationStatusUnknown }];
        X509ChainStatus[] unknown = [new() { Status = X509ChainStatusFlags.RevocationStatusUnknown }];
        var verification = new ServerCertificateVerification(
            new TlsClientOptions(CaCertificateFile: caFile, RevocationCheckBestEffort: bestEffort), true, TimeProvider.System)
        {
            FetchOcspResponse = (uri, _) => ocsp(uri),
            FetchRevocationList = (uri, _) => { fetchedLists?.Add(uri); return pki.CrlBytes; },
            RevocationDiagnosticLog = log ?? NoDiagnosticLog.Instance,
            ReadChainStatus = _ => partial,
            ReadCertificatesBelowAnchorStatus = _ => unknown,
        };
        verification = isProxy ? verification.ForProxy() : verification;
        var (chainPolicy, _, _) = verification.ReadTrustAnchors();
        chainPolicy!.RevocationMode = X509RevocationMode.NoCheck;
        chainPolicy.ExtraStore.Add(pki.Intermediate);
        using var chain = new X509Chain { ChainPolicy = chainPolicy };
        chain.Build(pki.Leaf);
        return verification.VerifyPeer(SslPolicyErrors.RemoteCertificateChainErrors, chain, Host, []);
    }

    // root -> intermediate (the --cacert file) -> leaf naming the OCSP responders, with the
    // intermediate's CRL and a distribution point to it unless there is none.
    private sealed class Pki : IDisposable
    {
        private readonly ECDsa _intermediateKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);

        private readonly X509Certificate2 _root;

        public Pki(List list, params string[] responders)
        {
            var now = DateTimeOffset.UtcNow;
            using var rootKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            _root = Authority("CN=Ocsp Root", rootKey).CreateSelfSigned(now.AddDays(-3), now.AddDays(30));
            using var signed = Authority("CN=Ocsp Intermediate", _intermediateKey).Create(_root, now.AddDays(-2), now.AddDays(20), [2]);
            Intermediate = signed.CopyWithPrivateKey(_intermediateKey);

            using var leafKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            var request = new X509CertificateRequest("CN=localhost", leafKey, HashAlgorithmName.SHA256);
            var names = new SubjectAlternativeNameBuilder();
            names.AddDnsName(Host);
            request.CertificateExtensions.Add(names.Build());
            request.CertificateExtensions.Add(new X509AuthorityInformationAccessExtension(responders, null));
            if (list != List.None)
            {
                request.CertificateExtensions.Add(CertificateRevocationListBuilder.BuildCrlDistributionPointExtension([CrlAddress]));
            }

            Leaf = request.Create(Intermediate, now.AddDays(-1), now.AddDays(10), [3]);
            var builder = new CertificateRevocationListBuilder();
            if (list == List.Revoking)
            {
                builder.AddEntry(Leaf);
            }

            CrlBytes = builder.Build(Intermediate, BigInteger.One, now.AddDays(5), HashAlgorithmName.SHA256);
        }

        public X509Certificate2 Intermediate { get; }

        public X509Certificate2 Leaf { get; }

        public byte[] CrlBytes { get; }

        public OcspResponseBuilder ResponseBuilder(OcspStapleStatus status) => new(Leaf.RawData, Intermediate.RawData, DateTimeOffset.UtcNow)
        {
            CertStatus = status,
            Signer = OcspResponseBuilder.EcdsaSigner(_intermediateKey),
            ResponderName = Intermediate.SubjectName.RawData,
        };

        public byte[] Response(OcspStapleStatus status) => ResponseBuilder(status).Build();

        // "good", signed by a responder the intermediate issued for OCSP signing, which the response carries.
        public byte[] DelegatedGoodResponse(TimeSpan responderExpiresIn, TimeSpan? responderStartsIn = null)
        {
            var now = DateTimeOffset.UtcNow;
            using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            var request = new X509CertificateRequest("CN=Ocsp Delegate", key, HashAlgorithmName.SHA256);
            request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension([new Oid(OcspTestPki.OcspSigningOid)], false));
            using var responder = request.Create(
                Intermediate.SubjectName,
                X509SignatureGenerator.CreateForECDsa(_intermediateKey),
                now + (responderStartsIn ?? TimeSpan.FromDays(-30)),
                now + responderExpiresIn,
                [4]);
            return (ResponseBuilder(OcspStapleStatus.Good) with
            {
                Signer = OcspResponseBuilder.EcdsaSigner(key),
                ResponderName = responder.SubjectName.RawData,
                Certificates = [responder.RawData],
            }).Build();
        }

        public void Dispose()
        {
            _root.Dispose();
            Intermediate.Dispose();
            Leaf.Dispose();
            _intermediateKey.Dispose();
        }

        private static X509CertificateRequest Authority(string subject, ECDsa key)
        {
            var request = new X509CertificateRequest(subject, key, HashAlgorithmName.SHA256);
            request.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
            request.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(request.PublicKey, false));
            return request;
        }
    }
}
