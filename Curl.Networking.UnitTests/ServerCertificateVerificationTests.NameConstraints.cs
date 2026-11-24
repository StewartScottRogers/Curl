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
    public void VerifyPeer_PermittedIpv6OnlyConstraintWithAnIpv4Leaf_IsSchannelError0x4000() =>
        Assert.AreEqual("schannel: error 0x00004000", Verify(Constraints(permittedIp: new byte[32]), "127.0.0.1", true, ipAddress: true)?.Message);

    [TestMethod]
    public void VerifyPeer_PermittedIpv4OnlyConstraintWithAnIpv6Leaf_IsSchannelError0x4000() =>
        Assert.AreEqual("schannel: error 0x00004000", Verify(Constraints(permittedIp: [10, 0, 0, 0, 255, 0, 0, 0]), "::1", true, ipAddress: true)?.Message);

    [TestMethod]
    public void VerifyPeer_ConstraintOnAnotherNameKind_Accepts() =>
        Assert.IsNull(Verify(Constraints(permittedDirectoryName: true), Host, true));

    [TestMethod]
    public void VerifyPeer_CnOnlyLeafOutsideThePermittedSubtree_IsSchannelError0x4000() =>
        Assert.AreEqual("schannel: error 0x00004000", Verify(Constraints(permitted: "example.com"), Host, true, withNames: false)?.Message);

    [TestMethod]
    public void VerifyPeer_CnOnlyLeafInsideTheExcludedSubtree_IsSchannelError0x8000() =>
        Assert.AreEqual("schannel: error 0x00008000", Verify(Constraints(excluded: "localhost"), Host, true, withNames: false)?.Message);

    [TestMethod]
    public void VerifyPeer_IpSanLeafWithAnOutsideCnUnderADnsConstraint_IsSchannelError0x4000() =>
        Assert.AreEqual("schannel: error 0x00004000", Verify(Constraints(permitted: "example.com"), "127.0.0.1", true, ipAddress: true, subject: "CN=localhost")?.Message);

    [TestMethod]
    public void VerifyPeer_IpSanLeafWithAPermittedCnUnderADnsConstraint_Accepts() =>
        Assert.IsNull(Verify(Constraints(permitted: "example.com"), "127.0.0.1", true, ipAddress: true, subject: "CN=www.example.com"));

    [TestMethod]
    public void VerifyPeer_DnsSanLeafIgnoresItsCn_Accepts() =>
        Assert.IsNull(Verify(Constraints(permitted: "example.com"), "www.example.com", true, subject: "CN=localhost"));

    [TestMethod]
    public void VerifyPeer_LeafWithoutACommonName_Accepts() =>
        Assert.IsNull(Verify(Constraints(permitted: "example.com"), Host, true, withNames: false, subject: "O=Nobody"));

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)] // OpenSSL-backed chain building checks every CN, multi-valued or not, against the constraint itself (BL-5497).
    public void VerifyPeer_LeafWithAMultiValuedRelativeName_IgnoresIt() =>
        Assert.IsNull(Verify(Constraints(permitted: "example.com"), Host, true, withNames: false, subject: "CN=localhost+OU=Unit"));

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
    public void VerifyPeer_ExcludedLeafWithUnknownRevocationStatus_IsRevocationStatusUnknown()
    {
        var failure = Verify(
            Constraints(excluded: "localhost"), Host, false, chainStatus: [new() { Status = X509ChainStatusFlags.RevocationStatusUnknown }]);

        Assert.AreEqual(CurlExitCode.PeerFailedVerification, failure?.ExitCode);
        Assert.AreEqual("schannel: the revocation status is unknown", failure?.Message);
    }

    [TestMethod]
    public void VerifyPeer_ExcludedLeafWithUnknownRevocationStatusUnderBestEffort_IsSchannelError0x8000() =>
        Assert.AreEqual(
            "schannel: error 0x00008000",
            Verify(
                Constraints(excluded: "localhost"), Host, false,
                chainStatus: [new() { Status = X509ChainStatusFlags.RevocationStatusUnknown }], bestEffort: true)?.Message);

    [TestMethod]
    public void VerifyPeer_ExcludedLeafWithUnknownRevocationStatusAndNoRevocationCheck_IsSchannelError0x8000() =>
        Assert.AreEqual(
            "schannel: error 0x00008000",
            Verify(Constraints(excluded: "localhost"), Host, true, chainStatus: [new() { Status = X509ChainStatusFlags.RevocationStatusUnknown }])?.Message);

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)] // OpenSSL-backed chain building reports the chain incomplete instead (BL-5420).
    public void VerifyPeer_MalformedNameConstraintsExtension_IsRefusedAsATrustError()
    {
        var failure = Verify([0x04, 0x01, 0xFF], Host, true);

        Assert.AreEqual(CurlExitCode.PeerFailedVerification, failure?.ExitCode);
        Assert.AreEqual("schannel: error 0x00004000", failure?.Message);
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)] // OpenSSL-backed chain building never hands the malformed chain over (BL-5420).
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

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)] // OpenSSL-backed chain building never hands the malformed chain over (BL-5420).
    public void FindViolation_MalformedLeafAlternativeNamesWithoutAnyConstraints_FindsNone()
    {
        using var root = CreateAuthority("CN=Constraint Root", null, null);
        using var intermediate = CreateAuthority("CN=Constraint Intermediate", root, null);
        using var leaf = CreateLeaf(intermediate, Host, false, true, true);
        using var chain = new X509Chain();
        chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
        chain.ChainPolicy.CustomTrustStore.Add(root);
        chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
        chain.ChainPolicy.ExtraStore.Add(intermediate);
        chain.Build(leaf);

        Assert.AreEqual(0, CertificateNameConstraints.FindViolation(chain));
    }

    [TestMethod]
    public void FindViolation_ConstraintsAboveTheListedAnchor_DoNotApply() =>
        Assert.AreEqual(0, FindViolationWithAnchorListed(anchorIsTheTopCertificate: false));

    [TestMethod]
    public void FindViolation_ConstraintsOfTheListedTopCertificate_StillApply() =>
        Assert.AreEqual(CertificateNameConstraints.NotPermitted, FindViolationWithAnchorListed(anchorIsTheTopCertificate: true));

    // root -> top (permitted example.com) -> sub (permitted localhost) -> leaf localhost, the server sending all of them (BL-5444).
    private static int FindViolationWithAnchorListed(bool anchorIsTheTopCertificate)
    {
        using var root = CreateAuthority("CN=Anchor Root", null, null);
        using var top = CreateAuthority("CN=Anchor Top", root, Constraints(permitted: "example.com"));
        using var sub = CreateAuthority("CN=Anchor Sub", top, Constraints(permitted: "localhost"));
        using var leaf = CreateLeaf(sub, Host, false, true, false);
        using var chain = new X509Chain();
        chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
        chain.ChainPolicy.CustomTrustStore.Add(root);
        chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
        chain.ChainPolicy.ExtraStore.Add(top);
        chain.ChainPolicy.ExtraStore.Add(sub);
        chain.Build(leaf);
        chain.ChainPolicy.CustomTrustStore.Clear();
        chain.ChainPolicy.CustomTrustStore.Add(anchorIsTheTopCertificate ? top : sub);

        return CertificateNameConstraints.FindViolation(chain);
    }

    private const string NotPermitted = "schannel: error 0x00004000";

    private const string Excluded = "schannel: error 0x00008000";

    [TestMethod]
    public void VerifyPeer_SubjectOutsideThePermittedDirectoryName_IsSchannelError0x4000() =>
        Assert.AreEqual(NotPermitted, Verify(Constraints(permittedName: DirectoryName("O=Good")), Host, true, subject: "O=Evil")?.Message);

    [TestMethod]
    public void VerifyPeer_SubjectEqualToThePermittedDirectoryName_Accepts() =>
        Assert.IsNull(Verify(Constraints(permittedName: DirectoryName("O=Good")), Host, true, subject: "O=Good"));

    [TestMethod]
    public void VerifyPeer_SubjectShorterThanThePermittedDirectoryName_IsSchannelError0x4000()
    {
        var good = ReadRelativeNames("O=Good");
        var extra = ReadRelativeNames("CN=Extra");

        Assert.AreEqual(NotPermitted, Verify(Constraints(permittedName: DirectoryName(good, extra)), Host, true, subject: "O=Good")?.Message);
    }

    [TestMethod]
    public void VerifyPeer_SubjectEqualToTheExcludedDirectoryName_IsSchannelError0x8000() =>
        Assert.AreEqual(Excluded, Verify(Constraints(excludedName: DirectoryName("O=Evil")), Host, true, subject: "O=Evil")?.Message);

    [TestMethod]
    public void VerifyPeer_EmptySubjectUnderAPermittedDirectoryName_Accepts() =>
        Assert.IsNull(Verify(Constraints(permittedName: DirectoryName("O=Good")), Host, true, subject: "", alternativeNames: AlternativeNames(GeneralName(2, Host))));

    [TestMethod]
    public void VerifyPeer_AlternativeDirectoryNameOutsideThePermittedDirectoryName_IsSchannelError0x4000() =>
        Assert.AreEqual(
            NotPermitted,
            Verify(Constraints(permittedName: DirectoryName("O=Good")), Host, true, subject: "O=Good", alternativeNames: AlternativeNames(GeneralName(2, Host), DirectoryName("O=Evil")))?.Message);

    [TestMethod]
    [DataRow("example.com", "evil@other.org", NotPermitted)]
    [DataRow("example.com", "a@example.com", null)]
    [DataRow(".example.com", "a@x.example.com", null)]
    [DataRow(".example.com", "a@example.com", NotPermitted)]
    [DataRow("a@example.com", "a@example.com", null)]
    [DataRow("a@example.com", "b@example.com", NotPermitted)]
    public void VerifyPeer_EmailAlternativeNameAgainstAPermittedEmailConstraint_FollowsTheConstraint(string constraint, string email, string? expected) =>
        Assert.AreEqual(expected, Verify(Constraints(permittedName: GeneralName(1, constraint)), Host, true, alternativeNames: AlternativeNames(GeneralName(2, Host), GeneralName(1, email)))?.Message);

    [TestMethod]
    public void VerifyPeer_SubjectEmailAddressOutsideThePermittedEmailDomain_IsSchannelError0x4000() =>
        Assert.AreEqual(NotPermitted, Verify(Constraints(permittedName: GeneralName(1, "example.com")), Host, true, subject: "E=evil@other.org, CN=localhost")?.Message);

    [TestMethod]
    public void VerifyPeer_SubjectEmailAddressInThePermittedEmailDomain_Accepts() =>
        Assert.IsNull(Verify(Constraints(permittedName: GeneralName(1, "example.com")), Host, true, subject: "E=a@example.com, CN=localhost"));

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)] // OpenSSL-backed chain building reads multi-valued names itself (BL-5497).
    public void VerifyPeer_SubjectEmailAddressInAMultiValuedRelativeName_IsIgnored() =>
        Assert.IsNull(Verify(Constraints(permittedName: GeneralName(1, "example.com")), Host, true, subject: "E=evil@other.org+OU=Unit, CN=localhost"));

    [TestMethod]
    public void VerifyPeer_SubjectEmailAddressInTheExcludedEmailDomain_IsSchannelError0x8000() =>
        Assert.AreEqual(Excluded, Verify(Constraints(excludedName: GeneralName(1, "other.org")), Host, true, subject: "E=evil@other.org, CN=localhost")?.Message);

    [TestMethod]
    public void VerifyPeer_EmailAlternativeNameInTheExcludedEmailDomain_IsSchannelError0x8000() =>
        Assert.AreEqual(Excluded, Verify(Constraints(excludedName: GeneralName(1, "other.org")), Host, true, alternativeNames: AlternativeNames(GeneralName(2, Host), GeneralName(1, "evil@other.org")))?.Message);

    [TestMethod]
    [DataRow(".example.com", "https://evil.other.org/", NotPermitted)]
    [DataRow(".example.com", "https://a.example.com/", null)]
    [DataRow("example.com", "https://example.com/x", null)]
    [DataRow("example.com", "https://a.example.com/", NotPermitted)]
    [DataRow(".example.com", "nothing", NotPermitted)]
    public void VerifyPeer_UriAlternativeNameAgainstAPermittedUriConstraint_FollowsTheConstraint(string constraint, string uri, string? expected) =>
        Assert.AreEqual(expected, Verify(Constraints(permittedName: GeneralName(6, constraint)), Host, true, alternativeNames: AlternativeNames(GeneralName(2, Host), GeneralName(6, uri)))?.Message);

    [TestMethod]
    public void VerifyPeer_UriAlternativeNameInTheExcludedUriHost_IsSchannelError0x8000() =>
        Assert.AreEqual(Excluded, Verify(Constraints(excludedName: GeneralName(6, "evil.other.org")), Host, true, alternativeNames: AlternativeNames(GeneralName(2, Host), GeneralName(6, "https://evil.other.org/")))?.Message);

    [TestMethod]
    public void VerifyPeer_SubjectDifferingOnlyInCaseFromTheExcludedDirectoryName_IsSchannelError0x8000() =>
        Assert.AreEqual(Excluded, Verify(Constraints(excludedName: DirectoryName("O=Evil")), Host, true, subject: "O=EVIL")?.Message);

    [TestMethod]
    public void VerifyPeer_SubjectDifferingOnlyInStringTypeFromTheExcludedDirectoryName_IsSchannelError0x8000() =>
        Assert.AreEqual(Excluded, Verify(Constraints(excludedName: DirectoryName(OrganizationName(UniversalTagNumber.UTF8String, "Evil"))), Host, true, subject: "O=Evil")?.Message);

    [TestMethod]
    public void VerifyPeer_SubjectDifferingOnlyInCaseFromThePermittedDirectoryName_Accepts() =>
        Assert.IsNull(Verify(Constraints(permittedName: DirectoryName(OrganizationName(UniversalTagNumber.UTF8String, "Good"))), Host, true, subject: "O=GOOD"));

    [TestMethod]
    public void VerifyPeer_SubjectWithAnotherAttributeValueThanTheExcludedDirectoryName_Accepts() =>
        Assert.IsNull(Verify(Constraints(excludedName: DirectoryName("O=Evil")), Host, true, subject: "O=Good"));

    [TestMethod]
    public void VerifyPeer_SubjectWithAnotherAttributeTypeThanTheExcludedDirectoryName_Accepts() =>
        Assert.IsNull(Verify(Constraints(excludedName: DirectoryName("O=Evil")), Host, true, subject: "OU=Evil"));

    [TestMethod]
    public void DirectoryNameMatches_ConstraintWithANonStringValue_DoesNotMatchAStringSubject()
    {
        var writer = new AsnWriter(AsnEncodingRules.DER);
        using (writer.PushSequence())
        {
            writer.WriteEncodedValue(RelativeName(attribute => attribute.WriteInteger(7)));
        }

        Assert.IsFalse(CertificateNameConstraints.DirectoryNameMatches(writer.Encode(), new X500DistinguishedName("O=Evil").RawData));
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)] // OpenSSL-backed chain building itself rejects a directoryName whose attribute value is context-specific (BL-5845).
    public void VerifyPeer_ExcludedDirectoryNameWithAContextSpecificValue_DoesNotMatchAStringSubject() =>
        Assert.IsNull(Verify(Constraints(excludedName: DirectoryName(RelativeName(writer => writer.WriteOctetString([1, 2], new Asn1Tag(TagClass.ContextSpecific, 0))))), Host, true, subject: "O=Evil"));

    [TestMethod]
    public void VerifyPeer_ExcludedDirectoryNameWithTwoAttributesInOneRelativeName_DoesNotMatchASubjectWithOne() =>
        Assert.IsNull(Verify(Constraints(excludedName: DirectoryName(RelativeName(writer => writer.WriteCharacterString(UniversalTagNumber.UTF8String, "Evil"), twoAttributes: true))), Host, true, subject: "O=Evil"));

    private static byte[] RelativeName(Action<AsnWriter> writeValue, bool twoAttributes = false)
    {
        var writer = new AsnWriter(AsnEncodingRules.DER);
        using (writer.PushSetOf())
        {
            using (writer.PushSequence())
            {
                writer.WriteObjectIdentifier("2.5.4.10");
                writeValue(writer);
            }

            if (twoAttributes)
            {
                using (writer.PushSequence())
                {
                    writer.WriteObjectIdentifier("2.5.4.11");
                    writeValue(writer);
                }
            }
        }

        return writer.Encode();
    }

    private static byte[] OrganizationName(UniversalTagNumber stringType, string value)
    {
        var writer = new AsnWriter(AsnEncodingRules.DER);
        using (writer.PushSetOf())
        using (writer.PushSequence())
        {
            writer.WriteObjectIdentifier("2.5.4.10");
            writer.WriteCharacterString(stringType, value);
        }

        return writer.Encode();
    }

    [TestMethod]
    public void VerifyPeer_AlternativeNameOfAnUnconstrainedKind_Accepts() =>
        Assert.IsNull(Verify(Constraints(permitted: Host), Host, true, alternativeNames: AlternativeNames(GeneralName(2, Host), [0x88, 0x01, 0x2A])));

    private static byte[] GeneralName(int tag, string value)
    {
        var writer = new AsnWriter(AsnEncodingRules.DER);
        writer.WriteCharacterString(UniversalTagNumber.IA5String, value, new Asn1Tag(TagClass.ContextSpecific, tag));
        return writer.Encode();
    }

    private static byte[] ReadRelativeNames(string name)
    {
        var rdns = new AsnReader(new X500DistinguishedName(name).RawData, AsnEncodingRules.DER).ReadSequence();
        return rdns.ReadEncodedValue().ToArray();
    }

    private static byte[] DirectoryName(string name) => DirectoryName(ReadRelativeNames(name));

    private static byte[] DirectoryName(params byte[][] relativeNames)
    {
        var writer = new AsnWriter(AsnEncodingRules.DER);
        using (writer.PushSequence(new Asn1Tag(TagClass.ContextSpecific, 4, true)))
        {
            using (writer.PushSequence())
            {
                foreach (var relativeName in relativeNames)
                {
                    writer.WriteEncodedValue(relativeName);
                }
            }
        }

        return writer.Encode();
    }

    private static byte[] AlternativeNames(params byte[][] generalNames)
    {
        var writer = new AsnWriter(AsnEncodingRules.DER);
        using (writer.PushSequence())
        {
            foreach (var generalName in generalNames)
            {
                writer.WriteEncodedValue(generalName);
            }
        }

        return writer.Encode();
    }

    private (CurlExitCode ExitCode, string Message)? Verify(
        byte[]? constraints, string host, bool skipRevocationCheck, bool ipAddress = false, bool withNames = true, bool malformedNames = false, string? subject = null, byte[]? alternativeNames = null, X509ChainStatus[]? chainStatus = null, bool bestEffort = false)
    {
        using var root = CreateAuthority("CN=Constraint Root", null, null);
        using var intermediate = CreateAuthority("CN=Constraint Intermediate", root, constraints);
        using var leaf = CreateLeaf(intermediate, host, ipAddress, withNames, malformedNames, subject, alternativeNames);
        var caFile = Path.Combine(_directory, "anchor.pem");
        File.WriteAllText(caFile, intermediate.ExportCertificatePem());
        var verification = new ServerCertificateVerification(
            new TlsClientOptions(CaCertificateFile: caFile, SkipRevocationCheck: skipRevocationCheck, RevocationCheckBestEffort: bestEffort), true, TimeProvider.System)
        {
            ReadChainStatus = _ => chainStatus ?? PartialChainStatus,
        };
        var (chainPolicy, _, _) = verification.ReadTrustAnchors();
        chainPolicy!.RevocationMode = X509RevocationMode.NoCheck;
        using var chain = new X509Chain { ChainPolicy = chainPolicy };
        chain.Build(leaf);
        return verification.VerifyPeer(SslPolicyErrors.RemoteCertificateChainErrors, chain, host, []);
    }

    private static byte[] Constraints(
        string? permitted = null, string? excluded = null, byte[]? permittedIp = null, byte[]? excludedIp = null,
        bool permittedDirectoryName = false, byte[]? permittedName = null, byte[]? excludedName = null)
    {
        var writer = new AsnWriter(AsnEncodingRules.DER);
        using (writer.PushSequence())
        {
            WriteSubtrees(writer, 0, permitted, permittedIp, permittedDirectoryName, permittedName);
            WriteSubtrees(writer, 1, excluded, excludedIp, false, excludedName);
        }

        return writer.Encode();
    }

    private static void WriteSubtrees(AsnWriter writer, int tag, string? dns, byte[]? ip, bool directoryName, byte[]? generalName = null)
    {
        if (dns is null && ip is null && !directoryName && generalName is null)
        {
            return;
        }

        using (writer.PushSequence(new Asn1Tag(TagClass.ContextSpecific, tag, true)))
        {
            using (writer.PushSequence())
            {
                if (generalName is not null)
                {
                    writer.WriteEncodedValue(generalName);
                }
                else if (dns is not null)
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

    private static X509Certificate2 CreateLeaf(X509Certificate2 issuer, string host, bool ipAddress, bool withNames, bool malformedNames, string? subject = null, byte[]? alternativeNames = null)
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest(subject ?? $"CN={host}", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        var rawNames = alternativeNames ?? (malformedNames ? new byte[] { 0x04, 0x01, 0xFF } : null);
        if (rawNames is not null)
        {
            request.CertificateExtensions.Add(new X509Extension(new Oid("2.5.29.17"), rawNames, false));
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
