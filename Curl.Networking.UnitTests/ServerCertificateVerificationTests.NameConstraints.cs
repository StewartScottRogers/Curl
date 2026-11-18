using System.Formats.Asn1;
using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

using Curl.Protocol.Abstractions;

namespace Curl.Networking;

/// <summary>
/// Pins that a <c>--cacert</c> file anchoring the chain at a non-self-signed CA still refuses
/// a server certificate outside that CA's name constraints (BL-5375): the Windows chain engine
/// reports only a partial chain there, and curl's Schannel build refuses with the trust error.
/// The proxy trust path builds the same verification from the proxy's TLS options.
/// </summary>
[TestClass]
public sealed class ServerCertificateVerificationNameConstraintTests
{
    private const string Host = "localhost";

    private static readonly X509ChainStatus[] PartialChainStatus = [new() { Status = X509ChainStatusFlags.PartialChain }];

    private string _directory = null!;

    [TestInitialize]
    public void CreateDirectory()
    {
        _directory = Path.Combine(Path.GetTempPath(), "curl-name-constraint-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_directory);
    }

    [TestCleanup]
    public void DeleteDirectory() => Directory.Delete(_directory, recursive: true);

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void VerifyPeer_LeafOutsideThePermittedSubtreeOfTheAnchor_IsSchannelError0x4000(bool skipRevocationCheck)
    {
        var failure = Verify(Constraints(permitted: "example.com"), Host, skipRevocationCheck);

        Assert.AreEqual(CurlExitCode.PeerFailedVerification, failure?.ExitCode);
        Assert.AreEqual("schannel: error 0x00004000", failure?.Message);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void VerifyPeer_LeafInsideTheExcludedSubtreeOfTheAnchor_IsSchannelError0x8000(bool skipRevocationCheck)
    {
        var failure = Verify(Constraints(excluded: "localhost"), Host, skipRevocationCheck);

        Assert.AreEqual(CurlExitCode.PeerFailedVerification, failure?.ExitCode);
        Assert.AreEqual("schannel: error 0x00008000", failure?.Message);
    }

    [TestMethod]
    public void VerifyPeer_LeafInsideThePermittedSubtreeOfTheAnchor_Accepts() =>
        Assert.IsNull(Verify(Constraints(permitted: "localhost"), Host, true));

    [TestMethod]
    public void VerifyPeer_LeafBelowAPermittedDomainOfTheAnchor_Accepts() =>
        Assert.IsNull(Verify(Constraints(permitted: "example.com"), "www.example.com", true));

    [TestMethod]
    public void VerifyPeer_LeafBelowADotPrefixedExcludedDomain_IsSchannelError0x8000() =>
        Assert.AreEqual("schannel: error 0x00008000", Verify(Constraints(excluded: ".example.com"), "a.example.com", true)?.Message);

    [TestMethod]
    public void VerifyPeer_UnconstrainedIntermediateAnchor_Accepts() =>
        Assert.IsNull(Verify(null, Host, false));

    [TestMethod]
    public void VerifyPeer_LeafWithAnIpAddressOutsideThePermittedRange_IsSchannelError0x4000() =>
        Assert.AreEqual("schannel: error 0x00004000", Verify(Constraints(permittedIp: [10, 0, 0, 0, 255, 0, 0, 0]), "127.0.0.1", true, ipAddress: true)?.Message);

    [TestMethod]
    public void VerifyPeer_LeafWithAnIpAddressInsideThePermittedRange_Accepts() =>
        Assert.IsNull(Verify(Constraints(permittedIp: [127, 0, 0, 0, 255, 0, 0, 0]), "127.0.0.1", true, ipAddress: true));

    [TestMethod]
    public void VerifyPeer_LeafWithAnIpAddressInsideTheExcludedRange_IsSchannelError0x8000() =>
        Assert.AreEqual("schannel: error 0x00008000", Verify(Constraints(excludedIp: [127, 0, 0, 0, 255, 0, 0, 0]), "127.0.0.1", true, ipAddress: true)?.Message);

    [TestMethod]
    public void VerifyPeer_IpConstraintOfAnotherFamily_Accepts() =>
        Assert.IsNull(Verify(Constraints(permittedIp: [10, 0, 0, 0, 255, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0]), "127.0.0.1", true, ipAddress: true));

    [TestMethod]
    public void VerifyPeer_ConstraintOnAnotherNameKind_Accepts() =>
        Assert.IsNull(Verify(Constraints(permittedDirectoryName: true), Host, true));

    [TestMethod]
    public void VerifyPeer_LeafWithoutAlternativeNamesUnderAConstrainedAnchor_Accepts() =>
        Assert.IsNull(Verify(Constraints(permitted: "example.com"), Host, true, withNames: false));

    [TestMethod]
    public void VerifyPeer_EmptyPermittedDomain_Accepts() =>
        Assert.IsNull(Verify(Constraints(permitted: ""), Host, true));

    [TestMethod]
    public void VerifyPeer_AddressConstraintsOnADnsLeaf_Accept() =>
        Assert.IsNull(Verify(Constraints(permittedIp: [10, 0, 0, 0, 255, 0, 0, 0], excludedIp: [127, 0, 0, 0, 255, 0, 0, 0]), Host, true));

    [TestMethod]
    public void VerifyPeer_ExcludedAddressRangeOfAnotherFamily_Accepts() =>
        Assert.IsNull(Verify(Constraints(excludedIp: new byte[32]), "127.0.0.1", true, ipAddress: true));

    [TestMethod]
    public void VerifyPeer_ExcludedDomainWithoutADotPrefixAboveTheLeaf_IsSchannelError0x8000() =>
        Assert.AreEqual("schannel: error 0x00008000", Verify(Constraints(excluded: "example.com"), "a.example.com", true)?.Message);

    [TestMethod]
    public void VerifyPeer_ExcludedDomainOfAnotherHost_Accepts() =>
        Assert.IsNull(Verify(Constraints(excluded: "example.com"), Host, true));

    [TestMethod]
    public void VerifyPeer_MalformedNameConstraintsExtension_IsRefusedAsATrustError()
    {
        var failure = Verify([0x04, 0x01, 0xFF], Host, true);

        Assert.AreEqual(CurlExitCode.PeerFailedVerification, failure?.ExitCode);
        Assert.AreEqual("schannel: error 0x00004000", failure?.Message);
    }

    [TestMethod]
    public void FindViolation_MalformedLeafAlternativeNames_RefusesTheChain()
    {
        using var root = CreateAuthority("CN=Constraint Root", null, null);
        using var intermediate = CreateAuthority("CN=Constraint Intermediate", root, Constraints(permitted: "localhost"));
        using var leaf = CreateLeaf(intermediate, Host, false, true, true);
        using var chain = new X509Chain();
        chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
        chain.ChainPolicy.CustomTrustStore.Add(root);
        chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
        chain.ChainPolicy.ExtraStore.Add(intermediate);
        chain.Build(leaf);

        Assert.AreEqual(CertificateNameConstraints.NotPermitted, CertificateNameConstraints.FindViolation(chain));
    }
    private (CurlExitCode ExitCode, string Message)? Verify(
        byte[]? constraints, string host, bool skipRevocationCheck, bool ipAddress = false, bool withNames = true, bool malformedNames = false)
    {
        using var root = CreateAuthority("CN=Constraint Root", null, null);
        using var intermediate = CreateAuthority("CN=Constraint Intermediate", root, constraints);
        using var leaf = CreateLeaf(intermediate, host, ipAddress, withNames, malformedNames);
        var caFile = Path.Combine(_directory, "anchor.pem");
        File.WriteAllText(caFile, intermediate.ExportCertificatePem());
        var verification = new ServerCertificateVerification(
            new TlsClientOptions(CaCertificateFile: caFile, SkipRevocationCheck: skipRevocationCheck), true, TimeProvider.System)
        {
            ReadChainStatus = _ => PartialChainStatus,
        };
        var (chainPolicy, _, _) = verification.ReadTrustAnchors();
        chainPolicy!.RevocationMode = X509RevocationMode.NoCheck;
        using var chain = new X509Chain { ChainPolicy = chainPolicy };
        chain.Build(leaf);
        return verification.VerifyPeer(SslPolicyErrors.RemoteCertificateChainErrors, chain, host, []);
    }

    private static byte[] Constraints(
        string? permitted = null, string? excluded = null, byte[]? permittedIp = null, byte[]? excludedIp = null,
        bool permittedDirectoryName = false)
    {
        var writer = new AsnWriter(AsnEncodingRules.DER);
        using (writer.PushSequence())
        {
            WriteSubtrees(writer, 0, permitted, permittedIp, permittedDirectoryName);
            WriteSubtrees(writer, 1, excluded, excludedIp, false);
        }

        return writer.Encode();
    }

    private static void WriteSubtrees(AsnWriter writer, int tag, string? dns, byte[]? ip, bool directoryName)
    {
        if (dns is null && ip is null && !directoryName)
        {
            return;
        }

        using (writer.PushSequence(new Asn1Tag(TagClass.ContextSpecific, tag, true)))
        {
            using (writer.PushSequence())
            {
                if (dns is not null)
                {
                    writer.WriteCharacterString(UniversalTagNumber.IA5String, dns, new Asn1Tag(TagClass.ContextSpecific, 2));
                }
                else if (ip is not null)
                {
                    writer.WriteOctetString(ip, new Asn1Tag(TagClass.ContextSpecific, 7));
                }
                else
                {
                    using (writer.PushSequence(new Asn1Tag(TagClass.ContextSpecific, 4, true)))
                    {
                        writer.WriteEncodedValue(new byte[] { 0x30, 0x00 });
                    }
                }
            }
        }
    }

    private static X509Certificate2 CreateAuthority(string subject, X509Certificate2? issuer, byte[]? nameConstraints)
    {
        var key = RSA.Create(2048);
        var request = new CertificateRequest(subject, key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
        request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.KeyCertSign, true));
        request.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(request.PublicKey, false));
        if (nameConstraints is not null)
        {
            request.CertificateExtensions.Add(new X509Extension(new Oid("2.5.29.30"), nameConstraints, true));
        }

        if (issuer is null)
        {
            return request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-2), DateTimeOffset.UtcNow.AddDays(30));
        }

        request.CertificateExtensions.Add(X509AuthorityKeyIdentifierExtension.CreateFromCertificate(issuer, true, false));
        using var unkeyed = request.Create(issuer, new DateTimeOffset(issuer.NotBefore), new DateTimeOffset(issuer.NotAfter), RandomNumberGenerator.GetBytes(8));
        return unkeyed.CopyWithPrivateKey(key);
    }

    private static X509Certificate2 CreateLeaf(X509Certificate2 issuer, string host, bool ipAddress, bool withNames, bool malformedNames)
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest($"CN={host}", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        if (malformedNames)
        {
            request.CertificateExtensions.Add(new X509Extension(new Oid("2.5.29.17"), new byte[] { 0x04, 0x01, 0xFF }, false));
        }
        else if (withNames)
        {
            var names = new SubjectAlternativeNameBuilder();
            if (ipAddress)
            {
                names.AddIpAddress(System.Net.IPAddress.Parse(host));
            }
            else
            {
                names.AddDnsName(host);
            }

            request.CertificateExtensions.Add(names.Build());
        }

        request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension([new Oid("1.3.6.1.5.5.7.3.1")], false));
        request.CertificateExtensions.Add(X509AuthorityKeyIdentifierExtension.CreateFromCertificate(issuer, true, false));
        return request.Create(issuer, DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(10), RandomNumberGenerator.GetBytes(8));
    }
}
