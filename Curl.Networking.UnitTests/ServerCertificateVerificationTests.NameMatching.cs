using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

using Curl.Protocol.Abstractions;

namespace Curl.Networking;

/// <summary>
/// Pins the host-name rules of curl's Schannel build on the <c>SslStream</c> route with
/// <c>--cacert</c> and <c>--ssl-no-revoke</c> (BL-5095): Windows' own name check lets a
/// partial-label wildcard such as <c>f*.a.test</c> and a dNSName holding an IP string match,
/// curl's does not, so the verdict stays exit 60 whatever <see cref="SslPolicyErrors" /> says.
/// </summary>
[TestClass]
public sealed class ServerCertificateVerificationNameMatchingTests
{
    [TestMethod]
    [DataRow("foo.a.test")]
    [DataRow("f.a.test")]
    [DataRow("fx.a.test")]
    [DataRow("FOO.a.test")]
    public void VerifyPeer_PartialLabelWildcardNameWindowsAccepted_IsExit60WithSchannelsMessage(string host)
    {
        using var authority = CreateAuthority();
        using var leaf = CreateLeaf(authority, names => names.AddDnsName("f*.a.test"));

        var failure = Verify(authority, leaf, host);

        Assert.AreEqual(CurlExitCode.PeerFailedVerification, failure?.ExitCode);
        Assert.AreEqual(
            $"schannel: CertGetNameString() failed to match connection hostname ({host}) against server certificate names",
            failure?.Message);
    }

    [TestMethod]
    [DataRow("127.0.0.1")]
    [DataRow("127.1")]
    public void VerifyPeer_IpHostAgainstDnsNameHoldingTheAddress_IsExit60(string host)
    {
        using var authority = CreateAuthority();
        using var leaf = CreateLeaf(authority, names => names.AddDnsName("127.0.0.1"));

        var failure = Verify(authority, leaf, host);

        Assert.AreEqual(CurlExitCode.PeerFailedVerification, failure?.ExitCode);
        Assert.AreEqual("SSL peer certificate or SSH remote key was not OK", failure?.Message);
    }

    [TestMethod]
    public void VerifyPeer_IpHostAgainstIpAddressName_Accepts()
    {
        using var authority = CreateAuthority();
        using var leaf = CreateLeaf(authority, names => names.AddIpAddress(System.Net.IPAddress.Loopback));

        Assert.IsNull(Verify(authority, leaf, "127.0.0.1"));
    }

    [TestMethod]
    public void VerifyPeer_WholeLabelWildcardAgainstASubdomain_StillAccepts()
    {
        using var authority = CreateAuthority();
        using var leaf = CreateLeaf(authority, names => names.AddDnsName("*.a.test"));

        Assert.IsNull(Verify(authority, leaf, "x.a.test"));
    }

    [TestMethod]
    public void VerifyPeer_DnsHostAgainstOnlyAnIpAddressName_LeavesTheCommonNameToDecide()
    {
        using var authority = CreateAuthority();
        using var leaf = CreateLeaf(authority, names => names.AddIpAddress(System.Net.IPAddress.Loopback));

        Assert.IsNull(Verify(authority, leaf, "foo.a.test"));
    }

    // Builds the chain with the policy ReadTrustAnchors read from the --cacert blob, as SslStream
    // would, and judges it with the name error Windows leaves out (none).
    private static (CurlExitCode ExitCode, string Message)? Verify(X509Certificate2 authority, X509Certificate2 leaf, string host)
    {
        var verification = new ServerCertificateVerification(
            new TlsClientOptions(
                SkipRevocationCheck: true,
                CaCertificateBlob: System.Text.Encoding.ASCII.GetBytes(authority.ExportCertificatePem())),
            matchesSchannelBuild: true,
            TimeProvider.System);
        var (chainPolicy, _, _) = verification.ReadTrustAnchors();
        using var chain = new X509Chain { ChainPolicy = chainPolicy! };
        chain.Build(leaf);
        return verification.VerifyPeer(SslPolicyErrors.None, chain, host, []);
    }

    private static X509Certificate2 CreateAuthority()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var request = new CertificateRequest("CN=Name Matching Test Root", key, HashAlgorithmName.SHA256);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
        request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.KeyCertSign, true));
        request.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(request.PublicKey, false));
        return request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-2), DateTimeOffset.UtcNow.AddDays(30));
    }

    private static X509Certificate2 CreateLeaf(X509Certificate2 issuer, Action<SubjectAlternativeNameBuilder> addNames)
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var request = new CertificateRequest("CN=name-matching-leaf.test", key, HashAlgorithmName.SHA256);
        var names = new SubjectAlternativeNameBuilder();
        addNames(names);
        request.CertificateExtensions.Add(names.Build());
        request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature, true));
        request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension([new Oid("1.3.6.1.5.5.7.3.1")], false));
        request.CertificateExtensions.Add(X509AuthorityKeyIdentifierExtension.CreateFromCertificate(issuer, true, false));
        return request.Create(issuer, DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(10), RandomNumberGenerator.GetBytes(8));
    }
}
