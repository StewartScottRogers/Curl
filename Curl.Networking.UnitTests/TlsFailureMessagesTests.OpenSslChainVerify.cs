using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace Curl.Networking;

/// <summary>
/// Pins the OpenSSL build's verify result for a chain built to a trusted root that fails a
/// later check: an invalid CA, an exceeded path length, a permitted subtree violation or a bad
/// signature (BL-5104), as curl 8.18.0's OpenSSL build prints it.
/// </summary>
public sealed partial class TlsFailureMessagesTests
{
    [TestMethod]
    [DataRow(X509ChainStatusFlags.UntrustedRoot | X509ChainStatusFlags.NotSignatureValid)]
    [DataRow(X509ChainStatusFlags.PartialChain | X509ChainStatusFlags.InvalidBasicConstraints)]
    [DataRow(X509ChainStatusFlags.NotTimeValid)]
    [DataRow(X509ChainStatusFlags.Revoked)]
    public void OpenSslExtensionOrSignatureError_UntrustedOrOtherStatus_IsNull(X509ChainStatusFlags status)
    {
        Diagnostics.Arrange("chain status", status);

        var message = TlsFailureMessages.OpenSslExtensionOrSignatureError(status, []);

        Diagnostics.Act("message", message ?? "null");
        Assert.IsNull(message);
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows | OperatingSystems.Linux)]
    public void OpenSslPeerFailedVerification_TrustedChainExceedingAPathLength_ReportsVerifyResult25()
    {
        var now = DateTimeOffset.UtcNow;
        using var rootKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var intermediateKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var leafKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var root = CreatePathLengthAuthority(rootKey, "CN=BL5104 Root", 0);
        using var intermediate = CreatePathLengthIssued(root, intermediateKey, "CN=BL5104 Intermediate", isAuthority: true, now);
        using var leaf = CreatePathLengthIssued(intermediate, leafKey, "CN=localhost", isAuthority: false, now);
        using var chain = CreateTrustingChain(root);
        chain.ChainPolicy.ExtraStore.Add(intermediate);
        chain.Build(leaf);
        Diagnostics.Arrange("chain", "root pathlen:0 -> intermediate CA -> leaf, root trusted");

        var message = TlsFailureMessages.OpenSslPeerFailedVerification(
            SslPolicyErrors.RemoteCertificateChainErrors, chain, "localhost");

        Diagnostics.Act("message", message);
        Diagnostics.Assert("message", "SSL certificate OpenSSL verify result: path length constraint exceeded (25)", message);
        Assert.AreEqual("SSL certificate OpenSSL verify result: path length constraint exceeded (25)", message);
    }

    private static X509Certificate2 CreatePathLengthAuthority(ECDsa key, string subject, int pathLength)
    {
        var request = new CertificateRequest(subject, key, HashAlgorithmName.SHA256);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, true, pathLength, true));
        request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.KeyCertSign | X509KeyUsageFlags.CrlSign, true));
        request.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(request.PublicKey, false));
        return request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-2), DateTimeOffset.UtcNow.AddDays(30));
    }

    private static X509Certificate2 CreatePathLengthIssued(X509Certificate2 issuer, ECDsa key, string subject, bool isAuthority, DateTimeOffset now)
    {
        var request = new CertificateRequest(subject, key, HashAlgorithmName.SHA256);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(isAuthority, false, 0, true));
        request.CertificateExtensions.Add(isAuthority
            ? new X509KeyUsageExtension(X509KeyUsageFlags.KeyCertSign | X509KeyUsageFlags.CrlSign, true)
            : new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature, true));
        request.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(request.PublicKey, false));
        request.CertificateExtensions.Add(X509AuthorityKeyIdentifierExtension.CreateFromCertificate(issuer, true, false));
        var serial = BitConverter.GetBytes(now.Ticks ^ subject.Length);
        using var issued = request.Create(issuer, now.AddDays(-1), now.AddDays(20), serial);
        return issued.CopyWithPrivateKey(key);
    }
}
