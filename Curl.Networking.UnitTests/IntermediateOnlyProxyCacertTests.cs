using System.Formats.Asn1;
using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

using Curl.Networking.Fakes;
using Curl.Protocol.Abstractions;

namespace Curl.Networking;

/// <summary>
/// Pins a <c>--proxy-cacert</c> file holding only the issuing intermediate (BL-5384): the Windows
/// chain engine reports every revocation status unknown there, so the CRL is read by the verifier.
/// </summary>
[TestClass]
public sealed class IntermediateOnlyProxyCacertTests
{
    private const string Host = "localhost";

    private const string UnreachableCrlAddress = "http://127.0.0.1:1/int.crl";

    private string _directory = null!;

    [TestInitialize]
    public void CreateDirectory()
    {
        _directory = Path.Combine(Path.GetTempPath(), "curl-int-proxy-cacert-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_directory);
    }

    [TestCleanup]
    public void DeleteDirectory() => Directory.Delete(_directory, recursive: true);

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    [DataRow(false)]
    [DataRow(true)]
    public void VerifyPeer_RealChainToTheIntermediateAnchor_Accepts(bool skipRevocationCheck)
    {
        using var pki = new Pki(revokeLeaf: false);

        var failure = VerifyRealChain(pki, pki.Intermediate, skipRevocationCheck, pki.CrlBytes);

        Assert.IsNull(failure);
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public void VerifyPeer_RealChainWithTheLeafRevokedByTheAnchorsCrl_IsExit60WithTheRevokedText()
    {
        using var pki = new Pki(revokeLeaf: true);

        var failure = VerifyRealChain(pki, pki.Intermediate, skipRevocationCheck: false, pki.CrlBytes);

        Assert.AreEqual(CurlExitCode.PeerFailedVerification, failure?.ExitCode);
        Assert.AreEqual(TlsFailureMessages.SchannelChainRevoked, failure?.Message);
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public void VerifyPeer_RealChainWithAnUnrelatedProxyCacert_IsExit60()
    {
        using var pki = new Pki(revokeLeaf: false);
        using var unrelated = Pki.CreateAuthority("CN=Unrelated", null, out _);

        var failure = VerifyRealChain(pki, unrelated, skipRevocationCheck: false, pki.CrlBytes);

        Assert.AreEqual(CurlExitCode.PeerFailedVerification, failure?.ExitCode);
    }

    [TestMethod]
    public void VerifyPeer_UnknownRevocationWithTheAnchorsValidCrl_Accepts()
    {
        using var pki = new Pki(revokeLeaf: false);

        Assert.IsNull(VerifyStubbed(pki, pki.CrlBytes));
    }

    [TestMethod]
    public void VerifyPeer_UnknownRevocationWithTheLeafOnTheAnchorsCrl_IsExit60WithTheRevokedText()
    {
        using var pki = new Pki(revokeLeaf: true);

        var failure = VerifyStubbed(pki, pki.CrlBytes);

        Assert.AreEqual(CurlExitCode.PeerFailedVerification, failure?.ExitCode);
        Assert.AreEqual(TlsFailureMessages.SchannelChainRevoked, failure?.Message);
    }

    [TestMethod]
    public void VerifyPeer_UnknownRevocationWithNoListToBeFetched_IsExit60WithoutTheRevokedText()
    {
        using var pki = new Pki(revokeLeaf: false);

        var failure = VerifyStubbed(pki, null);

        Assert.AreEqual(CurlExitCode.PeerFailedVerification, failure?.ExitCode);
        Assert.AreNotEqual(TlsFailureMessages.SchannelChainRevoked, failure?.Message);
    }

    [TestMethod]
    public void VerifyPeer_UnknownRevocationWithNoListToBeFetched_NamesTheUnknownRevocationStatus()
    {
        using var pki = new Pki(revokeLeaf: false);

        var failure = VerifyStubbed(pki, null);

        Assert.AreEqual(CurlExitCode.PeerFailedVerification, failure?.ExitCode);
        Assert.AreEqual("schannel: the revocation status is unknown", failure?.Message);
    }

    [TestMethod]
    public void VerifyPeer_UnknownRevocationWithAListThatDoesNotDecode_IsExit60()
    {
        using var pki = new Pki(revokeLeaf: false);

        Assert.AreEqual(CurlExitCode.PeerFailedVerification, VerifyStubbed(pki, [1, 2, 3])?.ExitCode);
    }

    [TestMethod]
    public void VerifyPeer_UnknownRevocationWithAListOfAnotherIssuer_IsExit60()
    {
        using var pki = new Pki(revokeLeaf: true);
        using var other = Pki.CreateAuthority("CN=Other Issuer", null, out var otherKey);

        var failure = VerifyStubbed(pki, Pki.BuildCrl(other, otherKey, [], DateTimeOffset.UtcNow.AddDays(3)));

        Assert.AreEqual(CurlExitCode.PeerFailedVerification, failure?.ExitCode);
        Assert.AreNotEqual(TlsFailureMessages.SchannelChainRevoked, failure?.Message);
    }

    [TestMethod]
    public void VerifyPeer_UnknownRevocationWithAListNotSignedByTheIssuersKey_IsExit60()
    {
        using var pki = new Pki(revokeLeaf: true);
        using var impostor = Pki.CreateAuthority(pki.Intermediate.Subject, null, out var impostorKey);

        var failure = VerifyStubbed(pki, Pki.BuildCrl(impostor, impostorKey, [pki.LeafSerial], DateTimeOffset.UtcNow.AddDays(3)));

        Assert.AreEqual(CurlExitCode.PeerFailedVerification, failure?.ExitCode);
        Assert.AreNotEqual(TlsFailureMessages.SchannelChainRevoked, failure?.Message);
    }

    [TestMethod]
    public void VerifyPeer_UnknownRevocationWithAnExpiredList_IsExit60()
    {
        using var pki = new Pki(revokeLeaf: false);

        var failure = VerifyStubbed(pki, pki.BuildCrlOfIssuer([], DateTimeOffset.UtcNow.AddHours(-1)));

        Assert.AreEqual(CurlExitCode.PeerFailedVerification, failure?.ExitCode);
    }

    [TestMethod]
    public void VerifyPeer_UnknownRevocationWithAListNamingNoNextUpdate_IsExit60()
    {
        using var pki = new Pki(revokeLeaf: false);

        Assert.AreEqual(CurlExitCode.PeerFailedVerification, VerifyStubbed(pki, pki.BuildCrlWithoutNextUpdate())?.ExitCode);
    }

    [TestMethod]
    public void VerifyPeer_UnknownRevocationOfALeafWithoutDistributionPoints_IsExit60()
    {
        using var pki = new Pki(revokeLeaf: false, withDistributionPoint: false);

        Assert.AreEqual(CurlExitCode.PeerFailedVerification, VerifyStubbed(pki, pki.CrlBytes)?.ExitCode);
    }

    [TestMethod]
    public void CrlDistributionPointUris_ExtensionHoldingRelativeNamesAndOtherNames_ListsOnlyHttpAddresses()
    {
        var writer = new AsnWriter(AsnEncodingRules.DER);
        using (writer.PushSequence())
        {
            using (writer.PushSequence())
            {
                using (writer.PushSequence(new Asn1Tag(TagClass.ContextSpecific, 0)))
                {
                    using (writer.PushSequence(new Asn1Tag(TagClass.ContextSpecific, 1)))
                    {
                        writer.WriteNull();
                    }

                    using (writer.PushSequence(new Asn1Tag(TagClass.ContextSpecific, 0)))
                    {
                        writer.WriteCharacterString(UniversalTagNumber.IA5String, "a@b.test", new Asn1Tag(TagClass.ContextSpecific, 1));
                        writer.WriteCharacterString(UniversalTagNumber.IA5String, "ldap://x.test/y", new Asn1Tag(TagClass.ContextSpecific, 6));
                        writer.WriteCharacterString(UniversalTagNumber.IA5String, "not a uri", new Asn1Tag(TagClass.ContextSpecific, 6));
                        writer.WriteCharacterString(UniversalTagNumber.IA5String, "http://x.test/a.crl", new Asn1Tag(TagClass.ContextSpecific, 6));
                    }
                }
            }
        }

        var uris = ServerCertificateVerification.CrlDistributionPointUris(CertificateWith(writer.Encode()));

        CollectionAssert.AreEqual(new[] { new Uri("http://x.test/a.crl") }, uris.ToArray());
    }

    [TestMethod]
    public void CrlDistributionPointUris_PointLimitedToReasons_ListsOnlyTheOtherPointsAddresses()
    {
        var writer = new AsnWriter(AsnEncodingRules.DER);
        using (writer.PushSequence())
        {
            WritePoint(writer, "http://x.test/partial.crl", withReasons: true);
            WritePoint(writer, "http://x.test/full.crl", withReasons: false);
        }

        var uris = ServerCertificateVerification.CrlDistributionPointUris(CertificateWith(writer.Encode()));

        CollectionAssert.AreEqual(new[] { new Uri("http://x.test/full.crl") }, uris.ToArray());
    }

    private static void WritePoint(AsnWriter writer, string address, bool withReasons)
    {
        using (writer.PushSequence())
        {
            using (writer.PushSequence(new Asn1Tag(TagClass.ContextSpecific, 0)))
            using (writer.PushSequence(new Asn1Tag(TagClass.ContextSpecific, 0)))
            {
                writer.WriteCharacterString(UniversalTagNumber.IA5String, address, new Asn1Tag(TagClass.ContextSpecific, 6));
            }

            if (withReasons)
            {
                writer.WriteBitString(new byte[] { 0x80 }, 7, new Asn1Tag(TagClass.ContextSpecific, 1));
            }
        }
    }
    [TestMethod]
    public void CrlDistributionPointUris_ExtensionThatDoesNotParse_ListsNone()
    {
        Assert.IsEmpty(ServerCertificateVerification.CrlDistributionPointUris(CertificateWith([1, 2, 3])));
    }

    [TestMethod]
    public void VerifyPeer_RevokedLeafChecked_FetchesEachListOnce()
    {
        using var pki = new Pki(revokeLeaf: true);
        var fetched = new List<Uri>();

        var failure = VerifyStubbedWith(pki, (uri, _) => { fetched.Add(uri); return pki.CrlBytes; }, NoDiagnosticLog.Instance, TimeSpan.FromSeconds(5));

        Assert.AreEqual(TlsFailureMessages.SchannelChainRevoked, failure?.Message);
        Assert.IsNotEmpty(fetched);
        Assert.AreEqual(fetched.Count, fetched.Distinct().Count());
    }

    private const string SecondPointAddress = "http://127.0.0.1:1/second.crl";

    [TestMethod]
    public void VerifyPeer_FirstUsableListIsCleanAndSecondRevokes_Accepts()
    {
        using var pki = new Pki(revokeLeaf: false, secondDistributionPoint: SecondPointAddress);
        var revoking = pki.BuildCrlOfIssuer([pki.LeafSerial], DateTimeOffset.UtcNow.AddDays(3));

        var failure = VerifyStubbedWith(
            pki, (uri, _) => uri.AbsolutePath == "/second.crl" ? revoking : pki.CrlBytes, NoDiagnosticLog.Instance, TimeSpan.FromSeconds(5));

        Assert.IsNull(failure);
    }

    [TestMethod]
    public void VerifyPeer_FirstUsableListRevokesAndSecondIsClean_IsRevoked()
    {
        using var pki = new Pki(revokeLeaf: true, secondDistributionPoint: SecondPointAddress);
        var clean = pki.BuildCrlOfIssuer([], DateTimeOffset.UtcNow.AddDays(3));

        var failure = VerifyStubbedWith(
            pki, (uri, _) => uri.AbsolutePath == "/second.crl" ? clean : pki.CrlBytes, NoDiagnosticLog.Instance, TimeSpan.FromSeconds(5));

        Assert.AreEqual(TlsFailureMessages.SchannelChainRevoked, failure?.Message);
    }

    [TestMethod]
    public void VerifyPeer_FirstListUnusableAndSecondRevokes_IsRevoked()
    {
        using var pki = new Pki(revokeLeaf: true, secondDistributionPoint: SecondPointAddress);

        var failure = VerifyStubbedWith(
            pki, (uri, _) => uri.AbsolutePath == "/second.crl" ? pki.CrlBytes : throw new IOException("404"), NoDiagnosticLog.Instance, TimeSpan.FromSeconds(5));

        Assert.AreEqual(TlsFailureMessages.SchannelChainRevoked, failure?.Message);
    }

    private const string DeltaAddress = "http://127.0.0.1:1/delta.crl";

    private const string FreshestCrlOid = "2.5.29.46";

    private static byte[] Number(byte value) => [0x02, 0x01, value];

    private static byte[] FreshestExtension(string address) =>
        TestRevocationList.Extension(FreshestCrlOid, false, CertificateRevocationListBuilder.BuildCrlDistributionPointExtension([address]).RawData);

    private static byte[] BaseListNumbered(Pki pki, byte number, params byte[][] more) =>
        TestRevocationList.Write(
            pki.Intermediate,
            nextUpdate: DateTimeOffset.UtcNow.AddDays(3),
            extensions: [TestRevocationList.Extension("2.5.29.20", false, Number(number)), .. more]);

    private static byte[] Delta(Pki pki, byte baseNumber, bool revoked, bool deltaIndicator = true) =>
        TestRevocationList.Write(
            pki.Intermediate,
            nextUpdate: DateTimeOffset.UtcNow.AddDays(3),
            revokedSerialNumbers: revoked ? [pki.LeafSerial] : null,
            extensions: deltaIndicator ? [TestRevocationList.Extension("2.5.29.27", true, Number(baseNumber))] : []);

    private (CurlExitCode ExitCode, string Message)? VerifyWithDelta(Pki pki, byte[] baseList, byte[]? delta) =>
        VerifyStubbedWith(
            pki,
            (uri, _) => uri.AbsolutePath == "/delta.crl" ? delta ?? throw new IOException("the server answered 404") : baseList,
            NoDiagnosticLog.Instance,
            TimeSpan.FromSeconds(5));

    [TestMethod]
    public void VerifyPeer_BaseListPointsToADeltaListRevokingTheLeaf_IsRevoked()
    {
        using var pki = new Pki(revokeLeaf: false);

        var failure = VerifyWithDelta(pki, BaseListNumbered(pki, 5, FreshestExtension(DeltaAddress)), Delta(pki, 5, revoked: true));

        Assert.AreEqual(TlsFailureMessages.SchannelChainRevoked, failure?.Message);
    }

    [TestMethod]
    public void VerifyPeer_CertificatePointsToADeltaListRevokingTheLeaf_IsRevoked()
    {
        using var pki = new Pki(revokeLeaf: false, freshestCrlAddress: DeltaAddress);

        var failure = VerifyWithDelta(pki, BaseListNumbered(pki, 5), Delta(pki, 4, revoked: true));

        Assert.AreEqual(TlsFailureMessages.SchannelChainRevoked, failure?.Message);
    }

    [TestMethod]
    public void VerifyPeer_DeltaListIsValidAndEmpty_Accepts()
    {
        using var pki = new Pki(revokeLeaf: false);

        var failure = VerifyWithDelta(pki, BaseListNumbered(pki, 5, FreshestExtension(DeltaAddress)), Delta(pki, 5, revoked: false));

        Assert.IsNull(failure);
    }

    [TestMethod]
    public void VerifyPeer_DeltaListCannotBeFetched_IsExit60AndNotRevoked()
    {
        using var pki = new Pki(revokeLeaf: false);

        var failure = VerifyWithDelta(pki, BaseListNumbered(pki, 5, FreshestExtension(DeltaAddress)), null);

        Assert.AreEqual(CurlExitCode.PeerFailedVerification, failure?.ExitCode);
        Assert.AreNotEqual(TlsFailureMessages.SchannelChainRevoked, failure?.Message);
    }

    [TestMethod]
    public void VerifyPeer_FreshestCrlNamesNoHttpAddress_IsExit60()
    {
        using var pki = new Pki(revokeLeaf: false);

        var failure = VerifyWithDelta(pki, BaseListNumbered(pki, 5, FreshestExtension("ldap://127.0.0.1/delta")), Delta(pki, 5, revoked: false));

        Assert.AreEqual(CurlExitCode.PeerFailedVerification, failure?.ExitCode);
    }

    [TestMethod]
    public void VerifyPeer_DeltaListNewerThanTheBaseList_IsExit60()
    {
        using var pki = new Pki(revokeLeaf: false);

        var failure = VerifyWithDelta(pki, BaseListNumbered(pki, 5, FreshestExtension(DeltaAddress)), Delta(pki, 6, revoked: false));

        Assert.AreEqual(CurlExitCode.PeerFailedVerification, failure?.ExitCode);
    }

    [TestMethod]
    public void VerifyPeer_BaseListWithoutANumberAndADelta_IsExit60()
    {
        using var pki = new Pki(revokeLeaf: false);
        var baseList = TestRevocationList.Write(pki.Intermediate, nextUpdate: DateTimeOffset.UtcNow.AddDays(3), extensions: [FreshestExtension(DeltaAddress)]);

        var failure = VerifyWithDelta(pki, baseList, Delta(pki, 5, revoked: false));

        Assert.AreEqual(CurlExitCode.PeerFailedVerification, failure?.ExitCode);
    }

    [TestMethod]
    public void VerifyPeer_FreshestCrlAnswersWithAListThatIsNotADelta_IsExit60()
    {
        using var pki = new Pki(revokeLeaf: false);

        var failure = VerifyWithDelta(pki, BaseListNumbered(pki, 5, FreshestExtension(DeltaAddress)), Delta(pki, 5, revoked: false, deltaIndicator: false));

        Assert.AreEqual(CurlExitCode.PeerFailedVerification, failure?.ExitCode);
    }

    [TestMethod]
    public void VerifyPeer_DeltaListThatIsExpiredOrNotTheIssuersOrSignedByAnother_IsExit60()
    {
        using var pki = new Pki(revokeLeaf: false);
        using var other = new Pki(revokeLeaf: false);
        var baseList = BaseListNumbered(pki, 5, FreshestExtension(DeltaAddress));
        var extensions = new[] { TestRevocationList.Extension("2.5.29.27", true, Number(5)) };
        var expired = TestRevocationList.Write(pki.Intermediate, thisUpdate: DateTimeOffset.UtcNow.AddDays(-3), nextUpdate: DateTimeOffset.UtcNow.AddDays(-1), extensions: extensions);
        var otherIssuer = TestRevocationList.Write(pki.Intermediate, new X500DistinguishedName("CN=Somebody Else"), nextUpdate: DateTimeOffset.UtcNow.AddDays(3), extensions: extensions);
        var otherSigner = TestRevocationList.Write(other.Intermediate, pki.Intermediate.SubjectName, nextUpdate: DateTimeOffset.UtcNow.AddDays(3), extensions: extensions);

        var undated = TestRevocationList.Write(pki.Intermediate, extensions: extensions);

        foreach (var delta in new[] { expired, otherIssuer, otherSigner, undated })
        {
            Assert.AreEqual(CurlExitCode.PeerFailedVerification, VerifyWithDelta(pki, baseList, delta)?.ExitCode);
        }
    }

    [TestMethod]
    public void VerifyPeer_StalledFetch_EndsWithinTheBudgetAsUnfetchable()
    {
        using var pki = new Pki(revokeLeaf: false);
        var started = System.Diagnostics.Stopwatch.StartNew();

        var failure = VerifyStubbedWith(
            pki,
            (_, token) => { token.WaitHandle.WaitOne(TimeSpan.FromSeconds(30)); token.ThrowIfCancellationRequested(); return []; },
            NoDiagnosticLog.Instance,
            TimeSpan.FromMilliseconds(100));

        Assert.AreEqual(CurlExitCode.PeerFailedVerification, failure?.ExitCode);
        Assert.IsLessThan(TimeSpan.FromSeconds(20), started.Elapsed);
    }

    [TestMethod]
    public void VerifyPeer_FailedFetch_IsWrittenToTheTlsLogAtWarning()
    {
        using var pki = new Pki(revokeLeaf: false);
        var log = new Fakes.RecordingDiagnosticLog(DiagnosticLogLevel.Warning);

        VerifyStubbedWith(pki, (_, _) => throw new IOException("the server answered 404"), log, TimeSpan.FromSeconds(5));

        var lines = log.At(DiagnosticLogLevel.Warning, DiagnosticLogComponents.Tls);
        Assert.IsNotEmpty(lines);
        Assert.AreEqual($"CRL {UnreachableCrlAddress} could not be fetched: the server answered 404", lines[0]);
    }

    [TestMethod]
    public void ForHandshake_FailedFetch_ReachesTheGivenLogAsATlsWarning()
    {
        using var pki = new Pki(revokeLeaf: false);
        var log = new Fakes.RecordingDiagnosticLog(DiagnosticLogLevel.Warning);

        VerifyStubbedWith(pki, (_, _) => throw new IOException("the server answered 503"), log, TimeSpan.FromSeconds(5), viaHandshake: true);

        var lines = log.At(DiagnosticLogLevel.Warning, DiagnosticLogComponents.Tls);
        Assert.IsNotEmpty(lines);
        Assert.AreEqual($"CRL {UnreachableCrlAddress} could not be fetched: the server answered 503", lines[0]);
    }

    [TestMethod]
    public void ForHandshake_StalledFetch_DoesNotOutlastTheConnectLimit()
    {
        using var pki = new Pki(revokeLeaf: false);
        using var connectLimit = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
        var started = System.Diagnostics.Stopwatch.StartNew();

        var failure = VerifyStubbedWith(
            pki,
            (_, token) => { token.WaitHandle.WaitOne(TimeSpan.FromSeconds(30)); token.ThrowIfCancellationRequested(); return []; },
            NoDiagnosticLog.Instance,
            TimeSpan.FromMinutes(5),
            viaHandshake: true,
            limit: connectLimit.Token);

        Assert.AreEqual(CurlExitCode.PeerFailedVerification, failure?.ExitCode);
        Assert.IsLessThan(TimeSpan.FromSeconds(20), started.Elapsed);
    }

    [TestMethod]
    public async Task AuthenticateAsClientAsync_WithALog_ValidatesItsArgumentsInBothProviders()
    {
        var events = new Fakes.RecordingTransferEvents();
        var sslStream = new SslStreamTlsProvider(new TlsClientOptions());
        var handBuilt = new HandBuiltTlsProvider(new TlsClientOptions(), TimeProvider.System);

        await Assert.ThrowsExactlyAsync<ArgumentNullException>(async () => await sslStream.AuthenticateAsClientAsync(null!, Host, events, false, [], NoDiagnosticLog.Instance, CancellationToken.None));
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(async () => await handBuilt.AuthenticateAsClientAsync(null!, Host, events, false, [], NoDiagnosticLog.Instance, CancellationToken.None));
    }

    private (CurlExitCode ExitCode, string Message)? VerifyStubbedWith(Pki pki, Func<Uri, CancellationToken, byte[]> fetch, IDiagnosticLog log, TimeSpan budget, bool viaHandshake = false, CancellationToken limit = default)
    {
        var caFile = Path.Combine(_directory, "proxy-cacert.pem");
        File.WriteAllText(caFile, pki.Intermediate.ExportCertificatePem());
        X509ChainStatus[] partial = [new() { Status = X509ChainStatusFlags.PartialChain | X509ChainStatusFlags.RevocationStatusUnknown }];
        X509ChainStatus[] unknown = [new() { Status = X509ChainStatusFlags.RevocationStatusUnknown }];
        var verification = new ServerCertificateVerification(new TlsClientOptions(CaCertificateFile: caFile), true, TimeProvider.System)
        {
            FetchRevocationList = fetch,
            RevocationDiagnosticLog = viaHandshake ? NoDiagnosticLog.Instance : log,
            RevocationFetchBudget = budget,
            ReadChainStatus = _ => partial,
            ReadCertificatesBelowAnchorStatus = _ => unknown,
        };
        verification = viaHandshake ? verification.ForHandshake(log, limit) : verification;
        var (chainPolicy, _, _) = verification.ReadTrustAnchors();
        chainPolicy!.RevocationMode = X509RevocationMode.NoCheck;
        chainPolicy.ExtraStore.Add(pki.Intermediate);
        using var chain = new X509Chain { ChainPolicy = chainPolicy };
        chain.Build(pki.Leaf);
        return verification.VerifyPeer(SslPolicyErrors.RemoteCertificateChainErrors, chain, Host, []);
    }

    private (CurlExitCode ExitCode, string Message)? VerifyRealChain(Pki pki, X509Certificate2 anchor, bool skipRevocationCheck, byte[]? list)
    {
        var verification = ProxyVerification(anchor, skipRevocationCheck, list);
        var (chainPolicy, _, _) = verification.ReadTrustAnchors();
        chainPolicy!.ExtraStore.Add(pki.Intermediate);
        using var chain = new X509Chain { ChainPolicy = chainPolicy };
        chain.Build(pki.Leaf);
        var errors = chain.ChainStatus.Length == 0 ? SslPolicyErrors.None : SslPolicyErrors.RemoteCertificateChainErrors;
        return verification.VerifyPeer(errors, chain, Host, []);
    }

    private (CurlExitCode ExitCode, string Message)? VerifyStubbed(Pki pki, byte[]? list)
    {
        var verification = ProxyVerification(pki.Intermediate, skipRevocationCheck: false, list, stubStatuses: true);
        var (chainPolicy, _, _) = verification.ReadTrustAnchors();
        chainPolicy!.RevocationMode = X509RevocationMode.NoCheck;
        chainPolicy.ExtraStore.Add(pki.Intermediate);
        using var chain = new X509Chain { ChainPolicy = chainPolicy };
        chain.Build(pki.Leaf);
        return verification.VerifyPeer(SslPolicyErrors.RemoteCertificateChainErrors, chain, Host, []);
    }

    private ServerCertificateVerification ProxyVerification(X509Certificate2 anchor, bool skipRevocationCheck, byte[]? list, bool stubStatuses = false)
    {
        var caFile = Path.Combine(_directory, "proxy-cacert.pem");
        File.WriteAllText(caFile, anchor.ExportCertificatePem());
        X509ChainStatus[] unknown = [new() { Status = X509ChainStatusFlags.RevocationStatusUnknown }];
        X509ChainStatus[] partial = [new() { Status = X509ChainStatusFlags.PartialChain | X509ChainStatusFlags.RevocationStatusUnknown }];
        var options = new TlsClientOptions(CaCertificateFile: caFile, SkipRevocationCheck: skipRevocationCheck);
        return stubStatuses
            ? new ServerCertificateVerification(options, true, TimeProvider.System)
            {
                FetchRevocationList = (_, _) => list ?? throw new IOException("no list"),
                ReadChainStatus = _ => partial,
                ReadCertificatesBelowAnchorStatus = _ => unknown,
            }
            : new ServerCertificateVerification(options, true, TimeProvider.System) { FetchRevocationList = (_, _) => list ?? throw new IOException("no list") };
    }

    private static X509Certificate2 CertificateWith(byte[] crlDistributionPoints)
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest("CN=Extension Holder", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509Extension(new Oid("2.5.29.31"), crlDistributionPoints, false));
        return request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
    }

    // root -> intermediate (the proxy's --proxy-cacert) -> leaf, with the intermediate's CRL.
    private sealed class Pki : IDisposable
    {
        private readonly X509Certificate2 _root;

        private readonly RSA _intermediateKey;

        public Pki(bool revokeLeaf, bool withDistributionPoint = true, string? freshestCrlAddress = null, string? secondDistributionPoint = null)
        {
            _root = CreateAuthority("CN=Proxy Root", null, out _);
            using var intermediate = CreateAuthority("CN=Proxy Intermediate", _root, out _intermediateKey);
            Intermediate = intermediate.CopyWithPrivateKey(_intermediateKey);
            LeafSerial = [0x42, 0x17];
            Leaf = CreateLeaf(Intermediate, LeafSerial, withDistributionPoint, freshestCrlAddress, secondDistributionPoint);
            CrlBytes = BuildCrlOfIssuer(revokeLeaf ? [LeafSerial] : [], DateTimeOffset.UtcNow.AddDays(3));
        }

        public X509Certificate2 Intermediate { get; }

        public X509Certificate2 Leaf { get; }

        public byte[] LeafSerial { get; }

        public byte[] CrlBytes { get; }

        public byte[] BuildCrlOfIssuer(byte[][] revokedSerials, DateTimeOffset nextUpdate) =>
            BuildCrl(Intermediate, _intermediateKey, revokedSerials, nextUpdate);

        // A signed CertificateList whose TBSCertList has no nextUpdate (RFC 5280 allows it).
        public byte[] BuildCrlWithoutNextUpdate()
        {
            var tbs = new AsnWriter(AsnEncodingRules.DER);
            using (tbs.PushSequence())
            {
                WriteSha256WithRsa(tbs);
                tbs.WriteEncodedValue(Intermediate.SubjectName.RawData);
                tbs.WriteUtcTime(DateTimeOffset.UtcNow.AddDays(-1));
            }

            var signature = _intermediateKey.SignData(tbs.Encode(), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            var list = new AsnWriter(AsnEncodingRules.DER);
            using (list.PushSequence())
            {
                list.WriteEncodedValue(tbs.Encode());
                WriteSha256WithRsa(list);
                list.WriteBitString(signature);
            }

            return list.Encode();
        }

        private static void WriteSha256WithRsa(AsnWriter writer)
        {
            using (writer.PushSequence())
            {
                writer.WriteObjectIdentifier("1.2.840.113549.1.1.11");
                writer.WriteNull();
            }
        }

        public static byte[] BuildCrl(X509Certificate2 issuer, RSA issuerKey, byte[][] revokedSerials, DateTimeOffset nextUpdate)
        {
            _ = issuerKey;
            var builder = new CertificateRevocationListBuilder();
            foreach (var serial in revokedSerials)
            {
                builder.AddEntry(serial, DateTimeOffset.UtcNow.AddHours(-1), X509RevocationReason.KeyCompromise);
            }

            return builder.Build(issuer, 1, nextUpdate, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1, nextUpdate.AddDays(-4));
        }

        public static X509Certificate2 CreateAuthority(string subject, X509Certificate2? issuer, out RSA key)
        {
            key = RSA.Create(2048);
            var request = new CertificateRequest(subject, key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            request.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
            request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.KeyCertSign | X509KeyUsageFlags.CrlSign, true));
            request.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(request.PublicKey, false));
            if (issuer is null)
            {
                return request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-2), DateTimeOffset.UtcNow.AddDays(30));
            }

            request.CertificateExtensions.Add(X509AuthorityKeyIdentifierExtension.CreateFromCertificate(issuer, true, false));
            return request.Create(issuer, DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(20), [1, 2, 3]);
        }

        private static X509Certificate2 CreateLeaf(X509Certificate2 issuer, byte[] serial, bool withDistributionPoint, string? freshestCrlAddress, string? secondDistributionPoint)
        {
            using var key = RSA.Create(2048);
            var request = new CertificateRequest($"CN={Host}", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            var names = new SubjectAlternativeNameBuilder();
            names.AddDnsName(Host);
            request.CertificateExtensions.Add(names.Build());
            request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension([new Oid("1.3.6.1.5.5.7.3.1")], false));
            request.CertificateExtensions.Add(X509AuthorityKeyIdentifierExtension.CreateFromCertificate(issuer, true, false));
            if (withDistributionPoint)
            {
                request.CertificateExtensions.Add(CertificateRevocationListBuilder.BuildCrlDistributionPointExtension(secondDistributionPoint is null ? [UnreachableCrlAddress] : [UnreachableCrlAddress, secondDistributionPoint]));
            }

            if (freshestCrlAddress is not null)
            {
                request.CertificateExtensions.Add(new X509Extension(new Oid(FreshestCrlOid), CertificateRevocationListBuilder.BuildCrlDistributionPointExtension([freshestCrlAddress]).RawData, false));
            }

            return request.Create(issuer, DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(10), serial);
        }

        public void Dispose()
        {
            Leaf.Dispose();
            Intermediate.Dispose();
            _intermediateKey.Dispose();
            _root.Dispose();
        }
    }
}
