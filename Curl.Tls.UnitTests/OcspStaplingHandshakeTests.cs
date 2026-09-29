using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace Curl.Tls;

/// <summary>
/// <c>--cert-status</c> over both handshakes against the in-memory servers: the client asks
/// for a stapled OCSP response, passes a good one, and fails every other with
/// <c>bad_certificate_status_response</c> and the <see cref="OcspStapleOutcome" /> the
/// caller maps to exit 91.
/// </summary>
[TestClass]
public sealed class OcspStaplingHandshakeTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 28, 12, 0, 0, TimeSpan.Zero);

    public enum StapleCase
    {
        Good,
        Revoked,
        Unknown,
        Missing,
        BadSignature,
        AnotherCertificate,
        Expired,
        UnauthorisedResponder,
    }

    public static IEnumerable<object[]> Cases =>
    [
        [StapleCase.Good, OcspStapleStatus.Good, 0],
        [StapleCase.Revoked, OcspStapleStatus.Revoked, 1],
        [StapleCase.Unknown, OcspStapleStatus.Unknown, 0],
        [StapleCase.Missing, OcspStapleStatus.NoResponse, 0],
        [StapleCase.BadSignature, OcspStapleStatus.SignatureInvalid, 0],
        [StapleCase.AnotherCertificate, OcspStapleStatus.CertificateNotFound, 0],
        [StapleCase.Expired, OcspStapleStatus.Expired, 0],
        [StapleCase.UnauthorisedResponder, OcspStapleStatus.ResponderNotAuthorised, 0],
    ];

    [TestMethod]
    [DynamicData(nameof(Cases))]
    public void Tls13ChecksTheResponseStapledToTheLeaf(StapleCase staple, OcspStapleStatus expected, int code)
    {
        using OcspTestPki pki = new();
        byte[]? response = Staple(pki, staple);
        Tls13TestServer server = new(pki.LeafCredential)
        {
            IssuerCertificates = [pki.Ca.RawData],
            LeafExtensions = response is null ? [] : [StatusRequestExtension.EncodeOcspResponse(response)],
        };
        using Tls13ClientHandshake client = HandshakeDriver.Client(HandshakeDriver.DefaultSettings with { RequestOcspStatus = true, TimeProvider = new FixedTimeProvider(Now) });

        Tls13HandshakeOutput output = HandshakeDriver.Run(client, server);

        AssertOutcome(new OcspStapleOutcome(expected, code), client.CertificateStatus, output.IsComplete, output.Failure);
    }

    [TestMethod]
    [DynamicData(nameof(Cases))]
    public void Tls12ChecksTheCertificateStatusMessage(StapleCase staple, OcspStapleStatus expected, int code)
    {
        using OcspTestPki pki = new();
        Tls12TestServer server = new(pki.LeafCredential) { IssuerCertificates = [pki.Ca.RawData], OcspResponse = Staple(pki, staple) };
        Tls12ClientHandshake client = Tls12HandshakeDriver.Client(Tls12HandshakeDriver.DefaultSettings with { RequestOcspStatus = true, TimeProvider = new FixedTimeProvider(Now) });

        Tls12HandshakeOutput output = Tls12HandshakeDriver.Run(client, server);

        AssertOutcome(new OcspStapleOutcome(expected, code), client.CertificateStatus, output.IsComplete, output.Failure);
    }

    [TestMethod]
    public void Tls13AsksForTheStatusInItsHello()
    {
        using Tls13ClientHandshake client = HandshakeDriver.Client(HandshakeDriver.DefaultSettings with { RequestOcspStatus = true });

        ClientHello hello = ClientHello.Decode(HandshakeMessageReader.Read(client.Start().BytesToSend[0].Bytes).Message!.Body).Value;

        TlsExtension statusRequest = hello.Extensions.Single(extension => extension.Type == TlsExtensionType.StatusRequest);
        CollectionAssert.AreEqual(new byte[] { 1, 0, 0, 0, 0 }, statusRequest.Data);
        Assert.AreEqual(TlsExtensionType.SupportedGroups, hello.Extensions[hello.Extensions.ToList().IndexOf(statusRequest) - 1].Type);
    }

    [TestMethod]
    public void Tls13RefusesToAskWithoutAPlaceForTheExtension()
    {
        Tls13ClientSettings settings = HandshakeDriver.DefaultSettings with { RequestOcspStatus = true, ExtensionOrder = [TlsExtensionType.SupportedVersions, TlsExtensionType.KeyShare] };

        Assert.ThrowsExactly<ArgumentException>(() => HandshakeDriver.Client(settings));
    }

    [TestMethod]
    public void WithoutCertStatusAStapledResponseIsNotChecked()
    {
        using OcspTestPki pki = new();
        byte[] revoked = (pki.Response(Now) with { CertStatus = OcspStapleStatus.Revoked }).Build();
        TlsExtension statusRequest = StatusRequestExtension.EncodeOcspRequest(new OcspStatusRequest([], []));
        Tls13TestServer server = new(pki.LeafCredential) { LeafExtensions = [StatusRequestExtension.EncodeOcspResponse(revoked)] };
        using Tls13ClientHandshake client = HandshakeDriver.Client(HandshakeDriver.DefaultSettings with { FixedExtensions = [statusRequest] });

        Tls13HandshakeOutput output = HandshakeDriver.Run(client, server);

        Assert.IsTrue(output.IsComplete);
        Assert.IsNull(client.CertificateStatus);
    }

    [TestMethod]
    public void AChainTheVerifierRejectsIsNeverStatusChecked()
    {
        using OcspTestPki pki = new();
        RecordingCertificateVerifier verifier = new(ServerCertificateVerdict.Rejected("untrusted"));
        Tls12TestServer server = new(pki.LeafCredential) { IssuerCertificates = [pki.Ca.RawData], OcspResponse = pki.Response(Now).Build() };
        Tls12ClientHandshake client = Tls12HandshakeDriver.Client(Tls12HandshakeDriver.DefaultSettings with { RequestOcspStatus = true }, verifier);

        Tls12HandshakeOutput output = Tls12HandshakeDriver.Run(client, server);

        Assert.AreEqual("untrusted", output.Failure!.CertificateRejection);
        Assert.IsNull(output.Failure.CertificateStatusRejection);
        Assert.IsNull(client.CertificateStatus);
    }

    private static void AssertOutcome(OcspStapleOutcome expected, OcspStapleOutcome? actual, bool isComplete, TlsHandshakeFailure? failure)
    {
        Assert.AreEqual(expected, actual);
        if (expected.IsGood)
        {
            Assert.IsTrue(isComplete);
            Assert.IsNull(failure);
            return;
        }

        Assert.IsFalse(isComplete);
        Assert.AreEqual(TlsAlertDescription.BadCertificateStatusResponse, failure!.Alert);
        Assert.AreEqual(expected, failure.CertificateStatusRejection);
        Assert.IsFalse(failure.IsCertificateRejection);
    }

    private static byte[]? Staple(OcspTestPki pki, StapleCase staple)
    {
        OcspResponseBuilder response = pki.Response(Now);
        return staple switch
        {
            StapleCase.Good => response.Build(),
            StapleCase.Revoked => (response with { CertStatus = OcspStapleStatus.Revoked, RevocationReason = 1 }).Build(),
            StapleCase.Unknown => (response with { CertStatus = OcspStapleStatus.Unknown }).Build(),
            StapleCase.Missing => null,
            StapleCase.BadSignature => (response with { CorruptSignature = true }).Build(),
            StapleCase.AnotherCertificate => (response with { SerialNumber = [0x09] }).Build(),
            StapleCase.Expired => (response with { ThisUpdateOffset = TimeSpan.FromDays(-2), NextUpdateOffset = TimeSpan.FromDays(-1) }).Build(),
            _ => UnauthorisedResponder(pki, response),
        };
    }

    private static byte[] UnauthorisedResponder(OcspTestPki pki, OcspResponseBuilder response)
    {
        using ECDsa key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using X509Certificate2 responder = pki.IssueResponder(key);
        return (response with
        {
            Signer = OcspResponseBuilder.EcdsaSigner(key),
            ResponderName = responder.SubjectName.RawData,
            Certificates = [responder.RawData],
        }).Build();
    }
}
