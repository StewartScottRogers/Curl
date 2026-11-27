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
    public void VerifyPeer_CnInAMultiValuedRelativeNameOutsideThePermittedSubtree_IsSchannelError0x4000() =>
        Assert.AreEqual(NotPermitted, Verify(Constraints(permitted: "example.com"), Host, true, withNames: false, subject: "CN=localhost+OU=Unit")?.Message);

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
            Constraints(excluded: "localhost"), Host, false, chainStatus: [new() { Status = X509ChainStatusFlags.RevocationStatusUnknown }],
            belowAnchorStatus: [new() { Status = X509ChainStatusFlags.RevocationStatusUnknown }]);

        Assert.AreEqual(CurlExitCode.PeerFailedVerification, failure?.ExitCode);
        Assert.AreEqual("schannel: the revocation status is unknown", failure?.Message);
    }

    [TestMethod]
    public void VerifyPeer_ExcludedLeafWhoseCrlResolvedBelowTheAnchor_IsSchannelError0x8000() =>
        Assert.AreEqual(
            "schannel: error 0x00008000",
            Verify(
                Constraints(excluded: "localhost"), Host, false,
                chainStatus: [new() { Status = X509ChainStatusFlags.RevocationStatusUnknown }], belowAnchorStatus: [])?.Message);

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
    public void VerifyPeer_SubjectEmailAddressInAMultiValuedRelativeNameInTheExcludedEmailDomain_IsSchannelError0x8000() =>
        Assert.AreEqual(Excluded, Verify(Constraints(excludedName: GeneralName(1, "other.org")), Host, true, subject: "E=evil@other.org+OU=Unit, CN=localhost")?.Message);

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
    public void VerifyPeer_SubjectMatchingAnExcludedUniversalStringDirectoryName_IsSchannelError0x8000() =>
        Assert.AreEqual(Excluded, Verify(Constraints(excludedName: DirectoryName(UniversalStringOrganizationName("eVIL "))), Host, true, subject: "O=Evil")?.Message);

    [TestMethod]
    public void VerifyPeer_SubjectMatchingAPermittedUniversalStringDirectoryName_Accepts() =>
        Assert.IsNull(Verify(Constraints(permittedName: DirectoryName(UniversalStringOrganizationName("Good"))), Host, true, subject: "O=Good"));

    [TestMethod]
    public void VerifyPeer_SubjectDifferingFromAPermittedUniversalStringDirectoryName_IsRefused() =>
        Assert.IsNotNull(Verify(Constraints(permittedName: DirectoryName(UniversalStringOrganizationName("Good"))), Host, true, subject: "O=Bad"));

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

    private static byte[] UniversalStringOrganizationName(string value)
    {
        var writer = new AsnWriter(AsnEncodingRules.DER);
        using (writer.PushSetOf())
        using (writer.PushSequence())
        {
            writer.WriteObjectIdentifier("2.5.4.10");
            var content = new System.Text.UTF32Encoding(true, false).GetBytes(value);
            writer.WriteEncodedValue([(byte)UniversalTagNumber.UniversalString, (byte)content.Length, .. content]);
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

    [TestMethod]
    [DataRow("localhost", null, "localhost")]
    [DataRow("localhost", "example.com", null)]
    [DataRow("Example Inc.", "example.com", null)]
    [DataRow("*.other.org", "example.com", null)]
    [DataRow(".other.org", "example.com", null)]
    [DataRow("other.org.", "example.com", null)]
    [DataRow("a..org", "example.com", null)]
    [DataRow("-a.org", "example.com", null)]
    [DataRow("a.org-", "example.com", null)]
    [DataRow("a-.org", "example.com", null)]
    [DataRow("a.-org", "example.com", null)]
    public void FindViolation_OpenSslBuildCnThatIsNoHostName_IsNotChecked(string commonName, string? permitted, string? excluded) =>
        Assert.AreEqual(0, FindViolationOfCommonName(commonName, Constraints(permitted: permitted, excluded: excluded)));

    [TestMethod]
    [DataRow("www.example.com")]
    [DataRow("www.example.com\0\0")]
    public void FindViolation_OpenSslBuildHostNameCnInsideTheExcludedSubtree_IsExcluded(string commonName) =>
        Assert.AreEqual(CertificateNameConstraints.Excluded, FindViolationOfCommonName(commonName, Constraints(excluded: "example.com")));

    [TestMethod]
    [DataRow("www.other.org")]
    [DataRow("a_b-1.other.org")]
    public void FindViolation_OpenSslBuildHostNameCnOutsideThePermittedSubtree_IsNotPermitted(string commonName) =>
        Assert.AreEqual(CertificateNameConstraints.NotPermitted, FindViolationOfCommonName(commonName, Constraints(permitted: "example.com")));

    [TestMethod]
    [DataRow("www\0.example.com")]
    [DataRow("a\0b")]
    public void FindViolation_OpenSslBuildCnHoldingAnInnerNul_IsUnsupportedNameSyntax(string commonName) =>
        Assert.AreEqual(CertificateNameConstraints.UnsupportedNameSyntax, FindViolationOfCommonName(commonName, Constraints(permitted: "example.com")));

    [TestMethod]
    public void FindViolation_OpenSslBuildCnHoldingANulUnderADirectoryNameConstraintOnly_IsUnsupportedNameSyntax() =>
        Assert.AreEqual(
            CertificateNameConstraints.UnsupportedNameSyntax,
            FindViolationOfCommonName("a\0b", Constraints(excludedName: DirectoryName("O=Nobody"))));

    [TestMethod]
    public void FindViolation_OpenSslBuildCnHoldingANulBesideAnExcludedEmail_ReportsTheOtherNameFirst() =>
        Assert.AreEqual(
            CertificateNameConstraints.Excluded,
            FindViolationOfSubject(
                Subject([(CommonName, "a\0b")]), Constraints(excludedName: GeneralName(1, "other.org")), alternativeNames: AlternativeNames(GeneralName(1, "evil@other.org"))));

    // curl 8.18.0's OpenSSL build (BL-5921): the CNs are checked in order, multi-valued relative names included.
    [TestMethod]
    [DataRow("www.example.com", "a\0b", CertificateNameConstraints.Excluded)]
    [DataRow("a\0b", "www.example.com", CertificateNameConstraints.UnsupportedNameSyntax)]
    [DataRow("a.safe.org", "a\0b", CertificateNameConstraints.UnsupportedNameSyntax)]
    public void FindViolation_OpenSslBuildTwoCns_StopsAtTheFirstRefused(string first, string second, int expected) =>
        Assert.AreEqual(expected, FindViolationOfSubject(Subject([(CommonName, first)], [(CommonName, second)]), Constraints(excluded: "example.com")));

    [TestMethod]
    public void FindViolation_OpenSslBuildDottedCnInAMultiValuedRelativeNameInsideTheExcludedSubtree_IsExcluded() =>
        Assert.AreEqual(
            CertificateNameConstraints.Excluded,
            FindViolationOfSubject(Subject([(CommonName, "www.example.com"), (OrganizationalUnit, "Unit")]), Constraints(excluded: "example.com")));

    [TestMethod]
    public void FindViolation_OpenSslBuildDottedCnInAMultiValuedRelativeNameOutsideThePermittedSubtree_IsNotPermitted() =>
        Assert.AreEqual(
            CertificateNameConstraints.NotPermitted,
            FindViolationOfSubject(Subject([(CommonName, "www.example.com"), (OrganizationalUnit, "Unit")]), Constraints(permitted: "other.org")));

    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.OSX)] // The verify result is read from the platform's own chain statuses, pinned on the Windows and Linux chain engines.
    public void VerifyPeer_OpenSslBuildCnHoldingAnInnerNulBeforeTheHostCn_IsUnsupportedNameSyntax()
    {
        var failure = Verify(
            Constraints(permitted: "example.com"), "www.example.com", true, withNames: false, matchesSchannelBuild: false,
            subjectName: Subject([(CommonName, "a\0b")], [(CommonName, "www.example.com")]));

        Assert.AreEqual(CurlExitCode.PeerFailedVerification, failure?.ExitCode);
        Assert.AreEqual("SSL certificate OpenSSL verify result: unsupported or invalid name syntax (53)", failure?.Message);
    }

    // ossl_verifyhost runs before the verify result is read, so a NUL in the matched CN is reported first.
    [TestMethod]
    [DataRow("www\0.example.com")]
    [DataRow("www.example.com\0")]
    public void VerifyPeer_OpenSslBuildOnlyCnHoldingANul_IsAnIllegalCertNameField(string commonName)
    {
        var failure = Verify(
            Constraints(permitted: "example.com"), "www.example.com", true, withNames: false, matchesSchannelBuild: false,
            subjectName: Subject([(CommonName, commonName)]));

        Assert.AreEqual(CurlExitCode.PeerFailedVerification, failure?.ExitCode);
        Assert.AreEqual("SSL: illegal cert name field", failure?.Message);
    }

    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.OSX)] // The verify result is read from the platform's own chain statuses, pinned on the Windows and Linux chain engines.
    public void VerifyPeer_OpenSslBuildDottedCnInAMultiValuedRelativeNameInsideTheExcludedSubtree_IsExcludedSubtreeViolation()
    {
        var failure = Verify(
            Constraints(excluded: "example.com"), "www.example.com", true, withNames: false, matchesSchannelBuild: false,
            subjectName: Subject([(CommonName, "www.example.com"), (OrganizationalUnit, "Unit")]));

        Assert.AreEqual(CurlExitCode.PeerFailedVerification, failure?.ExitCode);
        Assert.AreEqual("SSL certificate OpenSSL verify result: excluded subtree violation (48)", failure?.Message);
    }

    [TestMethod]
    public void MatchedCommonNameHoldsANul_NulInAnEarlierCnOnly_IsFalse()
    {
        using var certificate = CreateSelfSigned(Subject([(CommonName, "a\0b")], [(CommonName, "www.example.com")]), null);

        Assert.IsFalse(OpenSslCommonNameRefusal.MatchedCommonNameHoldsANul(certificate));
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public void MatchedCommonNameHoldsANul_DnsOrIpAlternativeName_IsFalse(bool ipAddress)
    {
        var names = new SubjectAlternativeNameBuilder();
        if (ipAddress)
        {
            names.AddIpAddress(System.Net.IPAddress.Loopback);
        }
        else
        {
            names.AddDnsName(Host);
        }

        using var certificate = CreateSelfSigned(Subject([(CommonName, "a\0b")]), names.Build());

        Assert.IsFalse(OpenSslCommonNameRefusal.MatchedCommonNameHoldsANul(certificate));
    }

    [TestMethod]
    public void MatchedCommonNameHoldsANul_NoCn_IsFalse()
    {
        using var certificate = CreateSelfSigned(Subject([(OrganizationalUnit, "Unit")]), null);

        Assert.IsFalse(OpenSslCommonNameRefusal.MatchedCommonNameHoldsANul(certificate));
    }

    [TestMethod]
    [DataRow(UniversalTagNumber.UTF8String)]
    [DataRow(UniversalTagNumber.BMPString)]
    public void DecodeCharacterString_CharacterString_IsItsText(UniversalTagNumber stringType)
    {
        var writer = new AsnWriter(AsnEncodingRules.DER);
        writer.WriteCharacterString(stringType, "a.org");

        CollectionAssert.AreEqual(new[] { "a.org" }, CertificateNameConstraints.DecodeCharacterString(writer.Encode()));
    }

    [TestMethod]
    public void DecodeCharacterString_UniversalString_IsItsText() =>
        CollectionAssert.AreEqual(new[] { "a" }, CertificateNameConstraints.DecodeCharacterString([0x1C, 0x04, 0, 0, 0, 0x61]));

    [TestMethod]
    [DataRow(new byte[] { 0x04, 0x01, 0x61 })]
    [DataRow(new byte[] { 0x8C, 0x01, 0x61 })]
    public void DecodeCharacterString_AnotherType_IsNothing(byte[] encoded) =>
        Assert.IsEmpty(CertificateNameConstraints.DecodeCharacterString(encoded));

    private const string CommonName = "2.5.4.3";

    private const string OrganizationalUnit = "2.5.4.11";

    private const string Organization = "2.5.4.10";

    private const string EmailAddress = "1.2.840.113549.1.9.1";

    // A subject of UTF8String attributes (IA5String emailAddresses, as OpenSSL requires), each array one relative name.
    private static X500DistinguishedName Subject(params (string Type, string Value)[][] relativeNames) =>
        SubjectWithEmailType(UniversalTagNumber.IA5String, relativeNames);

    private static X500DistinguishedName SubjectWithEmailType(UniversalTagNumber emailType, params (string Type, string Value)[][] relativeNames)
    {
        var writer = new AsnWriter(AsnEncodingRules.DER);
        using (writer.PushSequence())
        {
            foreach (var relativeName in relativeNames)
            {
                using (writer.PushSetOf())
                {
                    foreach (var (type, value) in relativeName)
                    {
                        using (writer.PushSequence())
                        {
                            writer.WriteObjectIdentifier(type);
                            writer.WriteCharacterString(type == EmailAddress ? emailType : UniversalTagNumber.UTF8String, value);
                        }
                    }
                }
            }
        }

        return new X500DistinguishedName(writer.Encode());
    }

    private static X509Certificate2 CreateSelfSigned(X500DistinguishedName subject, X509Extension? alternativeNames)
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest(subject, key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        if (alternativeNames is not null)
        {
            request.CertificateExtensions.Add(alternativeNames);
        }

        return request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(10));
    }

    [TestMethod]
    public void FindViolation_SchannelBuildDotlessCnInsideTheExcludedSubtree_IsExcluded() =>
        Assert.AreEqual(CertificateNameConstraints.Excluded, FindViolationOfCommonName("localhost", Constraints(excluded: "localhost"), matchesSchannelBuild: true));

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public void VerifyPeer_OpenSslBuildDotlessCnOnlyLeafUnderAConstrainedAnchor_Accepts(bool excluded)
    {
        var constraints = excluded ? Constraints(excluded: Host) : Constraints(permitted: "example.com");

        Assert.IsNull(Verify(constraints, Host, true, withNames: false, matchesSchannelBuild: false));
    }

    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.OSX)] // The verify result is read from the platform's own chain statuses, pinned on the Windows and Linux chain engines.
    public void VerifyPeer_OpenSslBuildDottedCnOnlyLeafInsideTheExcludedSubtree_IsExcludedSubtreeViolation()
    {
        var failure = Verify(Constraints(excluded: "example.com"), "www.example.com", true, withNames: false, matchesSchannelBuild: false);

        Assert.AreEqual(CurlExitCode.PeerFailedVerification, failure?.ExitCode);
        Assert.AreEqual("SSL certificate OpenSSL verify result: excluded subtree violation (48)", failure?.Message);
    }

    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.OSX)] // The partial chain is the platform engine's own status, pinned on the Windows and Linux chain engines.
    public void VerifyChain_OpenSslBuildDotlessCnOnlyLeafUnderAnExcludingCapathAnchor_Accepts()
    {
        using var root = CreateAuthority("CN=Capath Root", null, null);
        using var intermediate = CreateAuthority("CN=Capath Intermediate", root, Constraints(excluded: Host));
        using var leaf = CreateLeaf(intermediate, Host, false, false, false);
        File.WriteAllText(
            Path.Combine(_directory, OpenSslSubjectNameHash.Of(intermediate.SubjectName) + ".0"), intermediate.ExportCertificatePem());
        var verification = new ServerCertificateVerification(new TlsClientOptions(CaCertificateDirectory: _directory), false, TimeProvider.System);

        var verdict = verification.VerifyChain([leaf.RawData, intermediate.RawData], Host);

        Assert.IsNull(verdict.ErrorMessage);
    }

    // STD-191: the OpenSSL build reported "unable to get local issuer certificate (20)" for these.
    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.OSX)] // The verify result is read from the platform's own chain statuses, pinned on the Windows and Linux chain engines.
    public void VerifyPeer_OpenSslBuildUniversalStringSubjectInsideTheExcludedDirectoryName_IsExcludedSubtreeViolation()
    {
        var writer = new AsnWriter(AsnEncodingRules.DER);
        using (writer.PushSequence())
        {
            writer.WriteEncodedValue(UniversalStringOrganizationName("Evil"));
        }

        var failure = Verify(Constraints(excludedName: DirectoryName("O=Evil")), Host, true, subjectName: new X500DistinguishedName(writer.Encode()), matchesSchannelBuild: false);

        Assert.AreEqual(CurlExitCode.PeerFailedVerification, failure?.ExitCode);
        Assert.AreEqual("SSL certificate OpenSSL verify result: excluded subtree violation (48)", failure?.Message);
    }

    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.OSX)] // The verify result is read from the platform's own chain statuses, pinned on the Windows and Linux chain engines.
    public void VerifyPeer_OpenSslBuildSubjectEmailAddressInTheExcludedEmailDomain_IsExcludedSubtreeViolation()
    {
        var failure = Verify(Constraints(excludedName: GeneralName(1, "other.org")), Host, true, subject: "E=evil@other.org, CN=localhost", matchesSchannelBuild: false);

        Assert.AreEqual(CurlExitCode.PeerFailedVerification, failure?.ExitCode);
        Assert.AreEqual("SSL certificate OpenSSL verify result: excluded subtree violation (48)", failure?.Message);
    }

    // curl 8.21.0's Schannel build (BL-6007): multi-valued relative names are read, emailAddresses and CNs alike.
    [TestMethod]
    [DataRow(true, "other.org", "evil@other.org", CertificateNameConstraints.Excluded)]
    [DataRow(true, "other.org", "good@example.com", 0)]
    [DataRow(false, "example.com", "evil@other.org", CertificateNameConstraints.NotPermitted)]
    [DataRow(false, "example.com", "good@example.com", 0)]
    public void FindViolation_SchannelBuildSubjectEmailAddressInAMultiValuedRelativeName_FollowsTheConstraint(bool excluded, string constraint, string address, int expected)
    {
        var email = GeneralName(1, constraint);
        var constraints = excluded ? Constraints(excludedName: email) : Constraints(permittedName: email);

        Assert.AreEqual(expected, FindViolationOfSubject(Subject([(OrganizationalUnit, "Unit"), (EmailAddress, address)], [(CommonName, Host)]), constraints, matchesSchannelBuild: true));
    }

    [TestMethod]
    public void FindViolation_SchannelBuildCnInAMultiValuedRelativeNameInsideTheExcludedSubtree_IsExcluded() =>
        Assert.AreEqual(
            CertificateNameConstraints.Excluded,
            FindViolationOfSubject(Subject([(OrganizationalUnit, "Unit"), (CommonName, Host)]), Constraints(excluded: Host), matchesSchannelBuild: true));

    // curl 8.18.0's OpenSSL build (BL-5940): every subject emailAddress is checked, multi-valued relative names included.
    [TestMethod]
    [DataRow(true, "other.org", CertificateNameConstraints.Excluded)]
    [DataRow(false, "example.com", CertificateNameConstraints.NotPermitted)]
    [DataRow(false, "other.org", 0)]
    public void FindViolation_OpenSslBuildSubjectEmailAddressInAMultiValuedRelativeName_FollowsTheConstraint(bool excluded, string constraint, int expected)
    {
        var email = GeneralName(1, constraint);
        var constraints = excluded ? Constraints(excludedName: email) : Constraints(permittedName: email);

        Assert.AreEqual(expected, FindViolationOfSubject(Subject([(EmailAddress, "evil@other.org"), (OrganizationalUnit, "Unit")]), constraints));
    }

    [TestMethod]
    public void FindViolation_OpenSslBuildSecondEmailAddressOfOneRelativeNameInTheExcludedDomain_IsExcluded() =>
        Assert.AreEqual(
            CertificateNameConstraints.Excluded,
            FindViolationOfSubject(Subject([(EmailAddress, "a@example.com"), (EmailAddress, "evil@other.org")]), Constraints(excludedName: GeneralName(1, "other.org"))));

    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.OSX)] // The verify result is read from the platform's own chain statuses, pinned on the Windows and Linux chain engines.
    public void VerifyPeer_OpenSslBuildSubjectEmailAddressInAMultiValuedRelativeNameInTheExcludedEmailDomain_IsExcludedSubtreeViolation()
    {
        var failure = Verify(
            Constraints(excludedName: GeneralName(1, "other.org")), Host, true, matchesSchannelBuild: false,
            subjectName: Subject([(EmailAddress, "evil@other.org"), (OrganizationalUnit, "Unit")], [(CommonName, Host)]));

        Assert.AreEqual(CurlExitCode.PeerFailedVerification, failure?.ExitCode);
        Assert.AreEqual("SSL certificate OpenSSL verify result: excluded subtree violation (48)", failure?.Message);
    }

    // curl 8.18.0's OpenSSL build (BL-5942): a subject emailAddress that is not an IA5String answers 53, whatever the constraints' kinds.
    [TestMethod]
    [DataRow(UniversalTagNumber.UTF8String)]
    [DataRow(UniversalTagNumber.BMPString)]
    [DataRow(UniversalTagNumber.PrintableString)]
    public void FindViolation_OpenSslBuildSubjectEmailAddressThatIsNoIa5String_IsUnsupportedNameSyntax(UniversalTagNumber stringType) =>
        Assert.AreEqual(
            CertificateNameConstraints.UnsupportedNameSyntax,
            FindViolationOfSubject(SubjectWithEmailType(stringType, [(EmailAddress, "example.com")]), Constraints(excluded: "other.org")));

    [TestMethod]
    public void FindViolation_OpenSslBuildUtf8StringEmailAddressUnderAnExcludedDirectoryName_ReportsTheDirectoryNameFirst() =>
        Assert.AreEqual(
            CertificateNameConstraints.Excluded,
            FindViolationOfSubject(
                SubjectWithEmailType(UniversalTagNumber.UTF8String, [(Organization, "Evil")], [(EmailAddress, "a@example.com")]),
                Constraints(excludedName: DirectoryName("O=Evil"))));

    [TestMethod]
    public void FindViolation_OpenSslBuildUtf8StringEmailAddressBesideAnExcludedDnsName_IsUnsupportedNameSyntax() =>
        Assert.AreEqual(
            CertificateNameConstraints.UnsupportedNameSyntax,
            FindViolationOfSubject(
                SubjectWithEmailType(UniversalTagNumber.UTF8String, [(EmailAddress, "a@example.com")]),
                Constraints(excluded: "example.com"),
                alternativeNames: AlternativeNames(GeneralName(2, "www.example.com"))));

    [TestMethod]
    [DataRow(true, CertificateNameConstraints.Excluded)]
    [DataRow(false, CertificateNameConstraints.UnsupportedNameSyntax)]
    public void FindViolation_OpenSslBuildTwoEmailAddresses_StopsAtTheFirstRefused(bool ia5First, int expected)
    {
        var writer = new AsnWriter(AsnEncodingRules.DER);
        using (writer.PushSequence())
        {
            (UniversalTagNumber Type, string Value)[] emailAddresses =
                [(UniversalTagNumber.IA5String, "evil@other.org"), (UniversalTagNumber.UTF8String, "a@example.com")];
            foreach (var (type, value) in ia5First ? emailAddresses : emailAddresses.Reverse())
            {
                using (writer.PushSetOf())
                using (writer.PushSequence())
                {
                    writer.WriteObjectIdentifier(EmailAddress);
                    writer.WriteCharacterString(type, value);
                }
            }
        }

        Assert.AreEqual(
            expected,
            FindViolationOfSubject(new X500DistinguishedName(writer.Encode()), Constraints(excludedName: GeneralName(1, "other.org"))));
    }

    [TestMethod]
    public void FindViolation_SchannelBuildUtf8StringEmailAddressInsideThePermittedDomain_IsNotRefused() =>
        Assert.AreEqual(
            0,
            FindViolationOfSubject(
                SubjectWithEmailType(UniversalTagNumber.UTF8String, [(EmailAddress, "a@example.com")]),
                Constraints(permittedName: GeneralName(1, "example.com")),
                matchesSchannelBuild: true));

    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.OSX)] // The verify result is read from the platform's own chain statuses, pinned on the Windows and Linux chain engines.
    public void VerifyPeer_OpenSslBuildUtf8StringEmailAddressUnderNameConstraints_IsUnsupportedNameSyntax()
    {
        var failure = Verify(
            Constraints(permittedName: GeneralName(1, "example.com")), Host, true, matchesSchannelBuild: false,
            subjectName: SubjectWithEmailType(UniversalTagNumber.UTF8String, [(EmailAddress, "a@example.com")], [(CommonName, Host)]));

        Assert.AreEqual(CurlExitCode.PeerFailedVerification, failure?.ExitCode);
        Assert.AreEqual("SSL certificate OpenSSL verify result: unsupported or invalid name syntax (53)", failure?.Message);
    }

    // curl 8.18.0's OpenSSL build (BL-5950): names are taken one at a time, so the first refused name decides the verify result.
    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.OSX)] // The verify result is read from the platform's own chain statuses, pinned on the Windows and Linux chain engines.
    public void VerifyPeer_OpenSslBuildExcludedSubjectBeforeAnEmailAddressNotPermitted_IsExcludedSubtreeViolation()
    {
        var failure = Verify(
            Constraints(permittedName: GeneralName(1, "good.org"), excludedName: DirectoryName("O=Evil")), Host, true, matchesSchannelBuild: false,
            subjectName: Subject([(Organization, "Evil")], [(EmailAddress, "a@bad.org")], [(CommonName, Host)]));

        Assert.AreEqual(CurlExitCode.PeerFailedVerification, failure?.ExitCode);
        Assert.AreEqual("SSL certificate OpenSSL verify result: excluded subtree violation (48)", failure?.Message);
    }

    [TestMethod]
    [DataRow(true, false, CertificateNameConstraints.Excluded)]
    [DataRow(false, false, CertificateNameConstraints.NotPermitted)]
    [DataRow(true, true, CertificateNameConstraints.NotPermitted)]
    public void FindViolation_ExcludedEmailAndDnsNameNotPermitted_FollowsEachBuildsNameOrder(bool emailFirst, bool matchesSchannelBuild, int expected)
    {
        var email = GeneralName(1, "a@bad.org");
        var dnsName = GeneralName(2, Host);

        var violation = FindViolationOfSubject(
            Subject([(CommonName, Host)]), Constraints(permitted: "other.org", excludedName: GeneralName(1, "bad.org")), matchesSchannelBuild,
            AlternativeNames(emailFirst ? [email, dnsName] : [dnsName, email]));

        Assert.AreEqual(expected, violation);
    }

    [TestMethod]
    public void FindViolation_OpenSslBuildExcludedIpAddressBeforeADnsNameNotPermitted_IsExcluded() =>
        Assert.AreEqual(
            CertificateNameConstraints.Excluded,
            FindViolationOfSubject(
                Subject([(CommonName, Host)]), Constraints(permitted: "other.org", excludedIp: [10, 0, 0, 0, 255, 0, 0, 0]),
                alternativeNames: AlternativeNames([0x87, 0x04, 10, 1, 1, 1], GeneralName(2, Host))));

    [TestMethod]
    public void FindViolation_OpenSslBuildExcludedSubjectEmailAddressBeforeADnsNameNotPermitted_IsExcluded() =>
        Assert.AreEqual(
            CertificateNameConstraints.Excluded,
            FindViolationOfSubject(
                Subject([(EmailAddress, "a@bad.org")], [(CommonName, Host)]), Constraints(permitted: "other.org", excludedName: GeneralName(1, "bad.org")),
                alternativeNames: AlternativeNames(GeneralName(2, Host))));

    // curl 8.18.0's OpenSSL build (BL-5950): the constraining CAs are read from the top of the chain down.
    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.OSX)] // The verify result is read from the platform's own chain statuses, pinned on the Windows and Linux chain engines.
    [DataRow(true, "permitted subtree violation (47)")]
    [DataRow(false, "excluded subtree violation (48)")]
    public void VerifyPeer_OpenSslBuildTwoConstrainingAuthoritiesRefusingDifferently_ReportsTheUpperOnes(bool upperPermits, string expected)
    {
        var permitting = Constraints(permitted: "other.org");
        var excluding = Constraints(excluded: Host);
        using var root = CreateAuthority("CN=Two Constraints Root", null, null);
        using var anchor = CreateAuthority("CN=Two Constraints Anchor", root, upperPermits ? permitting : excluding);
        using var lower = CreateAuthority("CN=Two Constraints Lower", anchor, upperPermits ? excluding : permitting);
        using var leaf = CreateLeaf(lower, Host, false, true, false);
        var caFile = Path.Combine(_directory, "anchor.pem");
        File.WriteAllText(caFile, anchor.ExportCertificatePem());
        var verification = new ServerCertificateVerification(
            new TlsClientOptions(CaCertificateFile: caFile, SkipRevocationCheck: true), false, TimeProvider.System)
        {
            ReadChainStatus = _ => PartialChainStatus,
            ReadCertificatesBelowAnchorStatus = _ => [],
        };
        var (chainPolicy, _, _) = verification.ReadTrustAnchors();
        chainPolicy!.RevocationMode = X509RevocationMode.NoCheck;
        chainPolicy.ExtraStore.Add(lower);
        using var chain = new X509Chain { ChainPolicy = chainPolicy };
        chain.Build(leaf);

        var failure = verification.VerifyPeer(SslPolicyErrors.RemoteCertificateChainErrors, chain, Host, []);

        Assert.AreEqual(CurlExitCode.PeerFailedVerification, failure?.ExitCode);
        Assert.AreEqual($"SSL certificate OpenSSL verify result: {expected}", failure?.Message);
    }

    [TestMethod]
    [DataRow(true, false, CertificateNameConstraints.NotPermitted)]
    [DataRow(false, false, CertificateNameConstraints.Excluded)]
    [DataRow(true, true, CertificateNameConstraints.Excluded)]
    [DataRow(false, true, CertificateNameConstraints.NotPermitted)]
    public void FindViolation_TwoConstrainingAuthoritiesRefusingDifferently_FollowsEachBuildsAuthorityOrder(bool upperPermits, bool matchesSchannelBuild, int expected)
    {
        var permitting = Constraints(permitted: "other.org");
        var excluding = Constraints(excluded: Host);
        using var root = CreateAuthority("CN=Two Constraints Root", null, upperPermits ? permitting : excluding);
        using var intermediate = CreateAuthority("CN=Two Constraints Intermediate", root, upperPermits ? excluding : permitting);
        using var leaf = CreateLeaf(intermediate, Host, false, true, false);
        using var chain = new X509Chain();
        chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
        chain.ChainPolicy.CustomTrustStore.Add(root);
        chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
        chain.ChainPolicy.ExtraStore.Add(intermediate);
        chain.Build(leaf);

        Assert.AreEqual(expected, CertificateNameConstraints.FindViolation(chain, matchesSchannelBuild));
    }

    // curl 8.18.0's OpenSSL build (BL-5953): the intermediate's own names are checked against the CAs above it,
    // before the server certificate's, so they decide when the root is trusted. With the intermediate as the
    // anchor nothing stands above it. The Schannel build checks the server certificate's names only.
    [TestMethod]
    [DataRow(false, false, false, CertificateNameConstraints.Excluded)]
    [DataRow(false, true, false, CertificateNameConstraints.NotPermitted)]
    [DataRow(true, false, false, CertificateNameConstraints.NotPermitted)]
    [DataRow(true, true, false, CertificateNameConstraints.Excluded)]
    [DataRow(false, false, true, CertificateNameConstraints.NotPermitted)]
    [DataRow(true, false, true, CertificateNameConstraints.Excluded)]
    public void FindViolation_IntermediateAndLeafBothRefused_FollowsEachBuildsCheckedNames(
        bool rootPermits, bool intermediateIsAnchor, bool matchesSchannelBuild, int expected)
    {
        var rootConstraints = rootPermits
            ? Constraints(permittedName: GeneralName(1, "good.org"))
            : Constraints(excludedName: GeneralName(1, "bad.org"));
        var intermediateConstraints = rootPermits ? Constraints(excluded: Host) : Constraints(permitted: "other.org");

        var violation = FindViolationBelowRoot(
            rootConstraints, "CN=Named Intermediate, E=ca@bad.org", intermediateConstraints, intermediateIsAnchor, matchesSchannelBuild);

        Assert.AreEqual(expected, violation);
    }

    // curl 8.18.0's OpenSSL build (BL-5953): a self-issued intermediate's names are not checked.
    [TestMethod]
    public void FindViolation_OpenSslBuildSelfIssuedIntermediateWithAnExcludedName_IsNotRefused() =>
        Assert.AreEqual(
            0,
            FindViolationBelowRoot(
                Constraints(excludedName: GeneralName(1, "bad.org")), "CN=Named Root, E=ca@bad.org", null, false, false,
                rootSubject: "CN=Named Root, E=ca@bad.org"));

    // curl 8.18.0's OpenSSL build (BL-5953): only the server certificate's CN is checked as a DNS name.
    [TestMethod]
    public void FindViolation_OpenSslBuildIntermediateWithAnExcludedCommonName_IsNotRefused() =>
        Assert.AreEqual(
            0,
            FindViolationBelowRoot(Constraints(excluded: "bad.example.com"), "CN=bad.example.com", null, false, false));

    // A leaf for the host under the intermediate, which the root issued; the trusted one is either of them.
    private static int FindViolationBelowRoot(
        byte[] rootConstraints, string intermediateSubject, byte[]? intermediateConstraints, bool intermediateIsAnchor, bool matchesSchannelBuild, string rootSubject = "CN=Named Root")
    {
        using var root = CreateAuthority(rootSubject, null, rootConstraints);
        using var intermediate = CreateAuthority(intermediateSubject, root, intermediateConstraints);
        using var leaf = CreateLeaf(intermediate, Host, false, true, false);
        using var chain = new X509Chain();
        chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
        chain.ChainPolicy.CustomTrustStore.Add(intermediateIsAnchor ? intermediate : root);
        chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
        chain.ChainPolicy.ExtraStore.Add(intermediate);
        chain.ChainPolicy.ExtraStore.Add(root);
        chain.Build(leaf);
        Assert.HasCount(3, chain.ChainElements);

        return CertificateNameConstraints.FindViolation(chain, matchesSchannelBuild);
    }

    // curl 8.18.0's OpenSSL build with a self-signed root as --cacert (BL-5953): the chain is refused for
    // both subtrees, and the answer is the one the upper CA's constraint gives the server certificate, or
    // the intermediate's own name when that is refused first.
    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.OSX)] // The verify result is read from the platform's own chain statuses, pinned on the Windows and Linux chain engines.
    [DataRow(false, "excluded subtree violation (48)")]
    [DataRow(true, "permitted subtree violation (47)")]
    public void VerifyPeer_OpenSslBuildRootAsCaCertificateRefusedForBothSubtrees_ReportsTheRootsRefusal(bool rootPermits, string expected)
    {
        var permitting = Constraints(permitted: "other.org");
        var excluding = Constraints(excluded: Host);
        using var root = CreateAuthority("CN=Self Signed Anchor Root", null, rootPermits ? permitting : excluding);
        using var intermediate = CreateAuthority("CN=Self Signed Anchor Intermediate", root, rootPermits ? excluding : permitting);
        using var leaf = CreateLeaf(intermediate, Host, false, true, false);
        var caFile = Path.Combine(_directory, "root.pem");
        File.WriteAllText(caFile, root.ExportCertificatePem());
        var verification = new ServerCertificateVerification(
            new TlsClientOptions(CaCertificateFile: caFile, SkipRevocationCheck: true), false, TimeProvider.System);
        var (chainPolicy, _, _) = verification.ReadTrustAnchors();
        chainPolicy!.RevocationMode = X509RevocationMode.NoCheck;
        chainPolicy.ExtraStore.Add(intermediate);
        using var chain = new X509Chain { ChainPolicy = chainPolicy };
        chain.Build(leaf);

        var failure = verification.VerifyPeer(SslPolicyErrors.RemoteCertificateChainErrors, chain, Host, []);

        Assert.AreEqual(CurlExitCode.PeerFailedVerification, failure?.ExitCode);
        Assert.AreEqual($"SSL certificate OpenSSL verify result: {expected}", failure?.Message);
    }

    // The Linux chain engine flags the lower CA's refusal beside, or instead of, the upper one's (BL-5954).
    [TestMethod]
    [DataRow(X509ChainStatusFlags.HasNotPermittedNameConstraint | X509ChainStatusFlags.HasExcludedNameConstraint, CertificateNameConstraints.Excluded, false, X509ChainStatusFlags.HasExcludedNameConstraint)]
    [DataRow(X509ChainStatusFlags.HasNotPermittedNameConstraint | X509ChainStatusFlags.HasExcludedNameConstraint, CertificateNameConstraints.NotPermitted, false, X509ChainStatusFlags.HasNotPermittedNameConstraint)]
    [DataRow(X509ChainStatusFlags.HasNotPermittedNameConstraint | X509ChainStatusFlags.PartialChain, CertificateNameConstraints.Excluded, true, X509ChainStatusFlags.HasExcludedNameConstraint)]
    [DataRow(X509ChainStatusFlags.HasExcludedNameConstraint | X509ChainStatusFlags.PartialChain, CertificateNameConstraints.UnsupportedNameSyntax, false, X509ChainStatusFlags.HasNotSupportedNameConstraint | X509ChainStatusFlags.PartialChain)]
    [DataRow(X509ChainStatusFlags.HasExcludedNameConstraint | X509ChainStatusFlags.PartialChain, 0, true, X509ChainStatusFlags.HasExcludedNameConstraint | X509ChainStatusFlags.PartialChain)]
    public void WithNameConstraintViolation_PlatformFlags_KeepsOnlyTheViolationFound(
        X509ChainStatusFlags platform, int violation, bool replacesPartialChain, X509ChainStatusFlags expected) =>
        Assert.AreEqual(expected, OpenSslVerifyResult.WithNameConstraintViolation(platform, violation, replacesPartialChain));

    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.OSX)] // The name-constraint flags are the platform chain engine's, pinned on the Windows and Linux ones.
    [DataRow(true, X509ChainStatusFlags.HasNotPermittedNameConstraint)]
    [DataRow(false, X509ChainStatusFlags.HasExcludedNameConstraint)]
    public void StatusOf_TwoAuthoritiesUnderATrustedRootRefusingDifferently_KeepsTheUpperRefusalOnly(bool upperPermits, X509ChainStatusFlags expected)
    {
        var permitting = Constraints(permitted: "other.org");
        var excluding = Constraints(excluded: Host);
        using var root = CreateAuthority("CN=Two Refusals Root", null, null);
        using var upper = CreateAuthority("CN=Two Refusals Upper", root, upperPermits ? permitting : excluding);
        using var lower = CreateAuthority("CN=Two Refusals Lower", upper, upperPermits ? excluding : permitting);
        using var leaf = CreateLeaf(lower, Host, false, true, false);
        using var chain = new X509Chain();
        chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
        chain.ChainPolicy.CustomTrustStore.Add(root);
        chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
        chain.ChainPolicy.ExtraStore.Add(upper);
        chain.ChainPolicy.ExtraStore.Add(lower);
        chain.Build(leaf);

        var status = OpenSslVerifyResult.StatusOf(chain);

        Assert.AreEqual(expected, status & (X509ChainStatusFlags.HasNotPermittedNameConstraint | X509ChainStatusFlags.HasExcludedNameConstraint));
    }

    private const string UserPrincipalName = "1.3.6.1.4.1.311.20.2.3";

    private const string OtherTypeId = "1.2.3.4";

    [TestMethod]
    [DataRow("user@evil.test", 0x8000)]
    [DataRow("user@good.test", 0)]
    public void VerifyPeer_LeafUpnAgainstTheAnchorsExcludedUpn_IsSchannelError0x8000OnlyWhenWithin(string upn, int violation)
    {
        var failure = Verify(
            Constraints(excludedName: OtherName(UserPrincipalName, Text("evil.test"))), Host, true,
            alternativeNames: DnsAnd(OtherName(UserPrincipalName, Text(upn))));

        Assert.AreEqual(violation == 0 ? null : "schannel: error 0x00008000", failure?.Message);
    }

    [TestMethod]
    [DataRow("user@good.test", 0x4000)]
    [DataRow("user@evil.test", 0)]
    public void VerifyPeer_LeafUpnAgainstTheAnchorsPermittedUpn_IsSchannelError0x4000OnlyWhenOutside(string upn, int violation)
    {
        var failure = Verify(
            Constraints(permittedName: OtherName(UserPrincipalName, Text("evil.test"))), Host, true,
            alternativeNames: DnsAnd(OtherName(UserPrincipalName, Text(upn))));

        Assert.AreEqual(violation == 0 ? null : "schannel: error 0x00004000", failure?.Message);
    }

    // Each row measured against CryptoAPI (BL-6011).
    [TestMethod]
    [DataRow(UserPrincipalName, "evil.test", "USER@EVIL.TEST", true)]
    [DataRow(UserPrincipalName, "evil.test", "evil.test", true)]
    [DataRow(UserPrincipalName, "evil.test", "a@b@evil.test", true)]
    [DataRow(UserPrincipalName, "evil.test", "@evil.test", true)]
    [DataRow(UserPrincipalName, "evil.test", "user@sub.evil.test", false)]
    [DataRow(UserPrincipalName, "evil.test", "user@notevil.test", false)]
    [DataRow(UserPrincipalName, "evil.test", "sub.evil.test", false)]
    [DataRow(UserPrincipalName, "evil.test", "xevil.test", false)]
    [DataRow(UserPrincipalName, "evil.test", "evil.test@good.test", false)]
    [DataRow(UserPrincipalName, "evil.test", "", false)]
    [DataRow(UserPrincipalName, ".evil.test", "user@a.sub.evil.test", true)]
    [DataRow(UserPrincipalName, ".evil.test", "sub.evil.test", true)]
    [DataRow(UserPrincipalName, ".evil.test", "user@evil.test", false)]
    [DataRow(UserPrincipalName, "@EVIL.test", "a@b@evil.test", true)]
    [DataRow(UserPrincipalName, "@evil.test", "user@sub.evil.test", false)]
    [DataRow(UserPrincipalName, "user@evil.test", "USER@evil.TEST", true)]
    [DataRow(UserPrincipalName, "user@evil.test", "xuser@evil.test", false)]
    [DataRow(UserPrincipalName, "user@", "user@evil.test", false)]
    [DataRow(UserPrincipalName, "", "user@evil.test", true)]
    [DataRow(OtherTypeId, "evil.test", "xevil.test", true)]
    [DataRow(OtherTypeId, "abc", "x@ABC", true)]
    [DataRow(OtherTypeId, "abc", "a@xabc", false)]
    [DataRow(OtherTypeId, "abc", "abcx", false)]
    [DataRow(OtherTypeId, "evil.test", "u@sub.evil.test", false)]
    [DataRow(OtherTypeId, "user@evil.test", "xuser@evil.test", false)]
    public void FindViolation_SchannelBuildOtherName_MatchesAsCryptoApiDoes(string typeId, string constraint, string name, bool within)
    {
        var constraintName = OtherName(typeId, Text(constraint));
        var alternativeNames = DnsAnd(OtherName(typeId, Text(name)));

        var excluded = FindViolationOfAlternativeNames(Constraints(excludedName: constraintName), alternativeNames);
        var permitted = FindViolationOfAlternativeNames(Constraints(permittedName: constraintName), alternativeNames);

        Assert.AreEqual(within ? CertificateNameConstraints.Excluded : 0, excluded);
        Assert.AreEqual(within ? 0 : CertificateNameConstraints.NotPermitted, permitted);
    }

    [TestMethod]
    public void FindViolation_SchannelBuildOtherNameOfAnotherTypeId_IsNotConstrained()
    {
        var constraintName = OtherName(OtherTypeId, Text("evil.test"));
        var alternativeNames = DnsAnd(OtherName(UserPrincipalName, Text("user@evil.test")));

        Assert.AreEqual(0, FindViolationOfAlternativeNames(Constraints(excludedName: constraintName), alternativeNames));
        Assert.AreEqual(0, FindViolationOfAlternativeNames(Constraints(permittedName: constraintName), alternativeNames));
    }

    [TestMethod]
    public void FindViolation_SchannelBuildOtherNameWhoseValueIsNotAString_MatchesNoConstraint()
    {
        var constraintName = OtherName(UserPrincipalName, Text("evil.test"));
        var alternativeNames = DnsAnd(OtherName(UserPrincipalName, Integer()));

        Assert.AreEqual(0, FindViolationOfAlternativeNames(Constraints(excludedName: constraintName), alternativeNames));
        Assert.AreEqual(
            CertificateNameConstraints.NotPermitted,
            FindViolationOfAlternativeNames(Constraints(permittedName: constraintName), alternativeNames));
    }

    [TestMethod]
    public void FindViolation_SchannelBuildPermittedOtherNameWhoseValueIsNotAString_IsNotSupported()
    {
        var constraints = Constraints(permittedName: OtherName(UserPrincipalName, Integer()));
        var alternativeNames = DnsAnd(OtherName(UserPrincipalName, Text("user@evil.test")));

        Assert.AreEqual(CertificateNameConstraints.UnsupportedNameSyntax, FindViolationOfAlternativeNames(constraints, alternativeNames));
    }

    [TestMethod]
    public void FindViolation_SchannelBuildExcludedOtherNameWhoseValueIsNotAString_Accepts()
    {
        var constraints = Constraints(excludedName: OtherName(UserPrincipalName, Integer()));
        var alternativeNames = DnsAnd(OtherName(UserPrincipalName, Integer()));

        Assert.AreEqual(0, FindViolationOfAlternativeNames(constraints, alternativeNames));
    }

    [TestMethod]
    public void FindViolation_SchannelBuildUnsupportedPermittedOtherNameAndAnExcludedName_IsExcluded()
    {
        var constraints = Constraints(permittedName: OtherName(UserPrincipalName, Integer()), excluded: Host);
        var alternativeNames = DnsAnd(OtherName(UserPrincipalName, Text("user@evil.test")));

        Assert.AreEqual(CertificateNameConstraints.Excluded, FindViolationOfAlternativeNames(constraints, alternativeNames));
    }

    // CryptoAPI, measured with X509Chain (BL-6046): an rfc822Name constraint starting '@' matches the end of the name.
    [TestMethod]
    [DataRow("@evil.test", "u@evil.test", true)]
    [DataRow("@evil.test", "A@B@EVIL.TEST", true)]
    [DataRow("@evil.test", "u@sub.evil.test", false)]
    [DataRow("@evil.test", "evil.test", false)]
    [DataRow("@", "u@", true)]
    [DataRow("@", "u@evil.test", false)]
    [DataRow(".a@evil.test", "x.a@evil.test", false)]
    public void FindViolation_SchannelBuildEmailConstraintStartingWithAt_MatchesTheEndOfTheName(string constraint, string email, bool within)
    {
        Assert.AreEqual(within ? CertificateNameConstraints.Excluded : 0, FindViolationOfExcludedEmail(constraint, email, matchesSchannelBuild: true));
        Assert.AreEqual(within ? 0 : CertificateNameConstraints.NotPermitted, FindViolationOfPermittedEmail(constraint, email, matchesSchannelBuild: true));
    }

    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.OSX)] // An empty constraint is not measured against macOS's certificate parser.
    [DataRow("u@evil.test")]
    [DataRow("evil.test")]
    public void FindViolation_SchannelBuildEmptyEmailConstraint_MatchesEveryName(string email)
    {
        Assert.AreEqual(CertificateNameConstraints.Excluded, FindViolationOfExcludedEmail(string.Empty, email, matchesSchannelBuild: true));
        Assert.AreEqual(0, FindViolationOfPermittedEmail(string.Empty, email, matchesSchannelBuild: true));
    }

    [TestMethod]
    [DataRow("u@evil.test", false)]
    [DataRow("@evil.test", true)]
    public void FindViolation_OpenSslBuildEmailConstraintStartingWithAt_MatchesOnlyThatText(string email, bool within)
    {
        Assert.AreEqual(within ? CertificateNameConstraints.Excluded : 0, FindViolationOfExcludedEmail("@evil.test", email, matchesSchannelBuild: false));
        Assert.AreEqual(within ? 0 : CertificateNameConstraints.NotPermitted, FindViolationOfPermittedEmail("@evil.test", email, matchesSchannelBuild: false));
    }

    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.OSX)] // An empty constraint is not measured against macOS's certificate parser.
    public void FindViolation_OpenSslBuildEmptyEmailConstraint_DoesNotMatchAMailbox()
    {
        Assert.AreEqual(0, FindViolationOfExcludedEmail(string.Empty, "u@evil.test", matchesSchannelBuild: false));
        Assert.AreEqual(CertificateNameConstraints.NotPermitted, FindViolationOfPermittedEmail(string.Empty, "u@evil.test", matchesSchannelBuild: false));
    }

    private static int FindViolationOfExcludedEmail(string constraint, string email, bool matchesSchannelBuild) =>
        FindViolationOfAlternativeNames(Constraints(excludedName: GeneralName(1, constraint)), DnsAnd(GeneralName(1, email)), matchesSchannelBuild);

    private static int FindViolationOfPermittedEmail(string constraint, string email, bool matchesSchannelBuild) =>
        FindViolationOfAlternativeNames(Constraints(permittedName: GeneralName(1, constraint)), DnsAnd(GeneralName(1, email)), matchesSchannelBuild);

    private const string SmtpUtf8Mailbox = "1.3.6.1.5.5.7.8.9";

    private static byte[] Mailbox(string address) => OtherName(SmtpUtf8Mailbox, Text(address));

    // The OpenSSL build's otherName rows below were measured with curl 8.18.0 and OpenSSL 3.5.5 (BL-6047, ADR-1442).
    [TestMethod]
    [DataRow(UserPrincipalName, "user@evil.test")]
    [DataRow(UserPrincipalName, "user@good.test")]
    [DataRow(OtherTypeId, "evil.test")]
    public void FindViolation_OpenSslBuildOtherNameUnderAConstraintOfItsTypeId_IsAnUnsupportedConstraintType(string typeId, string name)
    {
        var constraintName = OtherName(typeId, Text("evil.test"));
        var alternativeNames = DnsAnd(OtherName(typeId, Text(name)));

        var excluded = FindViolationOfAlternativeNames(Constraints(excludedName: constraintName), alternativeNames, false);
        var permitted = FindViolationOfAlternativeNames(Constraints(permittedName: constraintName), alternativeNames, false);

        Assert.AreEqual(CertificateNameConstraints.UnsupportedConstraintType, excluded);
        Assert.AreEqual(CertificateNameConstraints.UnsupportedConstraintType, permitted);
    }

    [TestMethod]
    [DataRow(UserPrincipalName, OtherTypeId)]
    [DataRow(UserPrincipalName, SmtpUtf8Mailbox)]
    [DataRow(SmtpUtf8Mailbox, UserPrincipalName)]
    [DataRow(SmtpUtf8Mailbox, SmtpUtf8Mailbox)]
    public void FindViolation_OpenSslBuildOtherNameNoOtherNameConstraintAppliesTo_IsNotConstrained(string constraintTypeId, string typeId)
    {
        var constraintName = OtherName(constraintTypeId, Text("good.test"));
        var alternativeNames = DnsAnd(OtherName(typeId, Text("user@evil.test")));

        Assert.AreEqual(0, FindViolationOfAlternativeNames(Constraints(excludedName: constraintName), alternativeNames, false));
        Assert.AreEqual(0, FindViolationOfAlternativeNames(Constraints(permittedName: constraintName), alternativeNames, false));
    }

    [TestMethod]
    public void FindViolation_OpenSslBuildSmtpUtf8ConstraintBesideAnRfc822Name_IsNotConstrained()
    {
        var constraintName = Mailbox("evil.test");
        var alternativeNames = DnsAnd(GeneralName(1, "user@good.test"));

        Assert.AreEqual(0, FindViolationOfAlternativeNames(Constraints(excludedName: constraintName), alternativeNames, false));
        Assert.AreEqual(0, FindViolationOfAlternativeNames(Constraints(permittedName: constraintName), alternativeNames, false));
    }

    [TestMethod]
    [DataRow("evil.test", "user@evil.test", true)]
    [DataRow("evil.test", "USER@EVIL.TEST", true)]
    [DataRow("evil.test", "a@b@evil.test", true)]
    [DataRow("evil.test", "@evil.test", true)]
    [DataRow("evil.test", "user@a.evil.test", false)]
    [DataRow(".evil.test", "user@evil.test", false)]
    [DataRow(".evil.test", "user@a.evil.test", false)]
    [DataRow(".evil.test", "user@a..evil.test", true)]
    [DataRow(".evil.test", "@..EVIL.test", true)]
    [DataRow(".evil.test", "..evil.test@", false)]
    [DataRow(".", "user@evil..", true)]
    [DataRow("@evil.test", "user@evil.test", false)]
    [DataRow("user@evil.test", "user@evil.test", false)]
    [DataRow("", "user@evil.test", false)]
    [DataRow("", "user@", true)]
    [DataRow("xn--bcher-kva.test", "user@bücher.test", true)]
    [DataRow("xn--bcher-kva.test", "user@BüCHER.test", true)]
    [DataRow("xn--bcher-kva.test", "user@bÜcher.test", false)]
    [DataRow("xn--bcher-kva.test", "user@xn--bcher-kva.test", false)]
    [DataRow("XN--bcher-kva.test", "user@bücher.test", false)]
    [DataRow("XN--bcher-kva.test", "user@xn--bcher-kva.test", true)]
    [DataRow(".xn--bcher-kva.test", "user@a..bücher.test", true)]
    [DataRow("xn--.test", "user@.test", true)]
    [DataRow("xn--abc-.test", "user@abc.test", true)]
    public void FindViolation_OpenSslBuildSmtpUtf8MailboxUnderAnRfc822NameConstraint_FollowsTheConstraint(string constraint, string mailbox, bool within)
    {
        var constraintName = GeneralName(1, constraint);
        var alternativeNames = DnsAnd(Mailbox(mailbox));

        var excluded = FindViolationOfAlternativeNames(Constraints(excludedName: constraintName), alternativeNames, false);
        var permitted = FindViolationOfAlternativeNames(Constraints(permittedName: constraintName), alternativeNames, false);

        Assert.AreEqual(within ? CertificateNameConstraints.Excluded : 0, excluded);
        Assert.AreEqual(within ? 0 : CertificateNameConstraints.NotPermitted, permitted);
    }

    [TestMethod]
    [DataRow("evil.test", "evil.test")]
    [DataRow(".evil.test", "..evil.test")]
    [DataRow("a\0b", "user@evil.test")]
    public void FindViolation_OpenSslBuildSmtpUtf8MailboxItCannotCompare_IsUnsupportedNameSyntax(string constraint, string mailbox) =>
        AssertEitherSubtreeAnswers(constraint, Mailbox(mailbox), CertificateNameConstraints.UnsupportedNameSyntax);

    [TestMethod]
    public void FindViolation_OpenSslBuildSmtpUtf8MailboxThatIsNoUtf8String_IsUnsupportedNameSyntax() =>
        AssertEitherSubtreeAnswers("evil.test", OtherName(SmtpUtf8Mailbox, Integer()), CertificateNameConstraints.UnsupportedNameSyntax);

    [TestMethod]
    [DataRow("xn--!!.test", 0)]
    [DataRow(".xn--!!.test", 0)]
    [DataRow("", 256)]
    [DataRow(".", 254)]
    public void FindViolation_OpenSslBuildSmtpUtf8MailboxUnderAConstraintThatDoesNotDecode_IsUnspecified(string start, int more)
    {
        var constraintName = GeneralName(1, start + new string('a', more));
        var alternativeNames = DnsAnd(Mailbox("user@evil.test"));

        var excluded = FindViolationWithoutChainBuild(Constraints(excludedName: constraintName), alternativeNames);
        var permitted = FindViolationWithoutChainBuild(Constraints(permittedName: constraintName), alternativeNames);

        Assert.AreEqual(CertificateNameConstraints.UnspecifiedError, excluded);
        Assert.AreEqual(CertificateNameConstraints.UnspecifiedError, permitted);
    }

    [TestMethod]
    [DataRow("", 255)]
    [DataRow(".", 253)]
    public void FindViolation_OpenSslBuildSmtpUtf8MailboxUnderTheLongestConstraint_IsCompared(string start, int more)
    {
        var constraints = Constraints(permittedName: GeneralName(1, start + new string('a', more)));

        var violation = FindViolationOfAlternativeNames(constraints, DnsAnd(Mailbox("user@evil.test")), false);

        Assert.AreEqual(CertificateNameConstraints.NotPermitted, violation);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void VerifyPeer_OpenSslBuildLeafUpnUnderTheAnchorsUpnConstraint_IsUnsupportedConstraintType(bool excluded)
    {
        var name = OtherName(UserPrincipalName, Text("evil.test"));
        var failure = Verify(
            excluded ? Constraints(excludedName: name) : Constraints(permittedName: name), Host, true,
            alternativeNames: DnsAnd(OtherName(UserPrincipalName, Text("user@good.test"))), matchesSchannelBuild: false);

        Assert.AreEqual(CurlExitCode.PeerFailedVerification, failure?.ExitCode);
        Assert.AreEqual("SSL certificate OpenSSL verify result: unsupported name constraint type (51)", failure?.Message);
    }

    [TestMethod]
    [DataRow(true, "evil.test", "user@evil.test", "excluded subtree violation (48)")]
    [DataRow(false, "good.test", "user@evil.test", "permitted subtree violation (47)")]
    [DataRow(false, "evil.test", "evil.test", "unsupported or invalid name syntax (53)")]
    [DataRow(false, "xn--!!", "user@evil.test", "unspecified certificate verification error (1)")]
    public void VerifyPeer_OpenSslBuildSmtpUtf8MailboxUnderTheAnchorsRfc822NameConstraint_IsTheVerifyResult(
        bool excluded, string constraint, string mailbox, string result)
    {
        var name = GeneralName(1, constraint);
        var failure = Verify(
            excluded ? Constraints(excludedName: name) : Constraints(permittedName: name), Host, true,
            alternativeNames: DnsAnd(Mailbox(mailbox)), matchesSchannelBuild: false);

        Assert.AreEqual(CurlExitCode.PeerFailedVerification, failure?.ExitCode);
        Assert.AreEqual($"SSL certificate OpenSSL verify result: {result}", failure?.Message);
    }

    [TestMethod]
    [DataRow(true, CertificateNameConstraints.UnsupportedConstraintType)]
    [DataRow(false, CertificateNameConstraints.Excluded)]
    public void FindViolation_OpenSslBuildUpnAndAnExcludedDnsName_AnswersForTheFirstAlternativeName(bool upnFirst, int expected)
    {
        var constraints = Constraints(excluded: "bad.test", permittedName: OtherName(UserPrincipalName, Text("x")));
        var upn = OtherName(UserPrincipalName, Text("x"));
        var dnsName = GeneralName(2, "bad.test");

        var violation = FindViolationOfAlternativeNames(
            constraints, upnFirst ? AlternativeNames(upn, dnsName) : AlternativeNames(dnsName, upn), false);

        Assert.AreEqual(expected, violation);
    }

    [TestMethod]
    public void FindViolation_OpenSslBuildUpnThatIsNoStringUnderItsConstraint_IsAnUnsupportedConstraintType()
    {
        var upn = OtherName(UserPrincipalName, Integer());

        var violation = FindViolationOfAlternativeNames(Constraints(excludedName: upn), DnsAnd(upn), false);

        Assert.AreEqual(CertificateNameConstraints.UnsupportedConstraintType, violation);
    }

    [TestMethod]
    public void FindViolation_OpenSslBuildRfc822NameUnderAUpnConstraint_IsNotConstrained()
    {
        var constraints = Constraints(permittedName: OtherName(UserPrincipalName, Text("evil.test")));

        Assert.AreEqual(0, FindViolationOfAlternativeNames(constraints, DnsAnd(GeneralName(1, "user@evil.test")), false));
    }

    [TestMethod]
    public void FindViolation_OpenSslBuildSmtpUtf8MailboxUnderADnsConstraintOnly_IsNotChecked()
    {
        var constraints = Constraints(permitted: Host);

        Assert.AreEqual(0, FindViolationOfAlternativeNames(constraints, DnsAnd(OtherName(SmtpUtf8Mailbox, Integer())), false));
    }

    private static void AssertEitherSubtreeAnswers(string constraint, byte[] otherName, int expected)
    {
        var constraintName = GeneralName(1, constraint);
        var alternativeNames = DnsAnd(otherName);

        Assert.AreEqual(expected, FindViolationOfAlternativeNames(Constraints(excludedName: constraintName), alternativeNames, false));
        Assert.AreEqual(expected, FindViolationOfAlternativeNames(Constraints(permittedName: constraintName), alternativeNames, false));
    }

    private static int FindViolationOfAlternativeNames(byte[] constraints, byte[] alternativeNames, bool matchesSchannelBuild = true) =>
        FindViolationOfSubject(new X500DistinguishedName($"CN={Host}"), constraints, matchesSchannelBuild, alternativeNames);

    // The OpenSSL build's check of the certificates themselves, with no platform chain build: .NET on Linux
    // throws from X509Chain.Build where OpenSSL's answer is X509_V_ERR_UNSPECIFIED.
    private static int FindViolationWithoutChainBuild(byte[] constraints, byte[] alternativeNames)
    {
        using var root = CreateAuthority("CN=Unbuilt Chain Root", null, null);
        using var intermediate = CreateAuthority("CN=Unbuilt Chain Intermediate", root, constraints);
        using var leaf = CreateLeaf(intermediate, Host, false, false, false, alternativeNames: alternativeNames);

        return CertificateNameConstraints.FindViolationAmong([leaf, intermediate, root], matchesSchannelBuild: false);
    }

    private static byte[] Text(string value)
    {
        var writer = new AsnWriter(AsnEncodingRules.DER);
        writer.WriteCharacterString(UniversalTagNumber.UTF8String, value);
        return writer.Encode();
    }

    private static byte[] Integer()
    {
        var writer = new AsnWriter(AsnEncodingRules.DER);
        writer.WriteInteger(5);
        return writer.Encode();
    }

    // A GeneralName otherName: [0] { type-id, [0] value }.
    private static byte[] OtherName(string typeId, byte[] value)
    {
        var tag = new Asn1Tag(TagClass.ContextSpecific, 0, true);
        var writer = new AsnWriter(AsnEncodingRules.DER);
        using (writer.PushSequence(tag))
        {
            writer.WriteObjectIdentifier(typeId);
            using (writer.PushSequence(tag))
            {
                writer.WriteEncodedValue(value);
            }
        }

        return writer.Encode();
    }

    // A subjectAltName holding DNS:localhost and then the general name given.
    private static byte[] DnsAnd(byte[] generalName)
    {
        var writer = new AsnWriter(AsnEncodingRules.DER);
        using (writer.PushSequence())
        {
            writer.WriteCharacterString(UniversalTagNumber.IA5String, Host, new Asn1Tag(TagClass.ContextSpecific, 2));
            writer.WriteEncodedValue(generalName);
        }

        return writer.Encode();
    }

    private static int FindViolationOfCommonName(string commonName, byte[] constraints, bool matchesSchannelBuild = false)
    {
        var subject = new X500DistinguishedNameBuilder();
        subject.AddCommonName(commonName);
        return FindViolationOfSubject(subject.Build(), constraints, matchesSchannelBuild);
    }

    private static int FindViolationOfSubject(X500DistinguishedName subject, byte[] constraints, bool matchesSchannelBuild = false, byte[]? alternativeNames = null)
    {
        using var root = CreateAuthority("CN=Common Name Root", null, null);
        using var intermediate = CreateAuthority("CN=Common Name Intermediate", root, constraints);
        using var leaf = CreateLeaf(intermediate, Host, false, false, false, alternativeNames: alternativeNames, subjectName: subject);
        using var chain = new X509Chain();
        chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
        chain.ChainPolicy.CustomTrustStore.Add(root);
        chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
        chain.ChainPolicy.ExtraStore.Add(intermediate);
        chain.Build(leaf);

        return CertificateNameConstraints.FindViolation(chain, matchesSchannelBuild);
    }

    private (CurlExitCode ExitCode, string Message)? Verify(
        byte[]? constraints, string host, bool skipRevocationCheck, bool ipAddress = false, bool withNames = true, bool malformedNames = false, string? subject = null, byte[]? alternativeNames = null, X509ChainStatus[]? chainStatus = null, bool bestEffort = false, X509ChainStatus[]? belowAnchorStatus = null, bool matchesSchannelBuild = true, X500DistinguishedName? subjectName = null)
    {
        using var root = CreateAuthority("CN=Constraint Root", null, null);
        using var intermediate = CreateAuthority("CN=Constraint Intermediate", root, constraints);
        using var leaf = CreateLeaf(intermediate, host, ipAddress, withNames, malformedNames, subject, alternativeNames, subjectName);
        var caFile = Path.Combine(_directory, "anchor.pem");
        File.WriteAllText(caFile, intermediate.ExportCertificatePem());
        var verification = new ServerCertificateVerification(
            new TlsClientOptions(CaCertificateFile: caFile, SkipRevocationCheck: skipRevocationCheck, RevocationCheckBestEffort: bestEffort), matchesSchannelBuild, TimeProvider.System)
        {
            ReadChainStatus = _ => chainStatus ?? PartialChainStatus,
            ReadCertificatesBelowAnchorStatus = _ => belowAnchorStatus ?? [],
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

    private static X509Certificate2 CreateLeaf(X509Certificate2 issuer, string host, bool ipAddress, bool withNames, bool malformedNames, string? subject = null, byte[]? alternativeNames = null, X500DistinguishedName? subjectName = null)
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest(subjectName ?? new X500DistinguishedName(subject ?? $"CN={host}"), key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
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
