using System.Formats.Asn1;
using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

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
    public void CrlDistributionPointUris_ExtensionHoldingReasonsRelativeNamesAndOtherNames_ListsOnlyHttpAddresses()
    {
        var writer = new AsnWriter(AsnEncodingRules.DER);
        using (writer.PushSequence())
        {
            using (writer.PushSequence())
            {
                writer.WriteBitString(new byte[] { 0x80 }, 7, new Asn1Tag(TagClass.ContextSpecific, 1));
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
    public void CrlDistributionPointUris_ExtensionThatDoesNotParse_ListsNone()
    {
        Assert.IsEmpty(ServerCertificateVerification.CrlDistributionPointUris(CertificateWith([1, 2, 3])));
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
                FetchRevocationList = _ => list,
                ReadChainStatus = _ => partial,
                ReadCertificatesBelowAnchorStatus = _ => unknown,
            }
            : new ServerCertificateVerification(options, true, TimeProvider.System) { FetchRevocationList = _ => list };
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

        public Pki(bool revokeLeaf, bool withDistributionPoint = true)
        {
            _root = CreateAuthority("CN=Proxy Root", null, out _);
            using var intermediate = CreateAuthority("CN=Proxy Intermediate", _root, out _intermediateKey);
            Intermediate = intermediate.CopyWithPrivateKey(_intermediateKey);
            LeafSerial = [0x42, 0x17];
            Leaf = CreateLeaf(Intermediate, LeafSerial, withDistributionPoint);
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

        private static X509Certificate2 CreateLeaf(X509Certificate2 issuer, byte[] serial, bool withDistributionPoint)
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
                request.CertificateExtensions.Add(CertificateRevocationListBuilder.BuildCrlDistributionPointExtension([UnreachableCrlAddress]));
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
