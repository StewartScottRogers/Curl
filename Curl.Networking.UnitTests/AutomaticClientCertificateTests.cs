using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

using Curl.Networking.Fakes;

namespace Curl.Networking;

/// <summary>
/// <see cref="AutomaticClientCertificate" />: which certificate of the user's personal store
/// <c>--ssl-auto-client-cert</c> presents (ADR-0191).
/// </summary>
[TestClass]
public sealed class AutomaticClientCertificateTests
{
    private const string ClientAuthentication = "1.3.6.1.5.5.7.3.2";

    private const string ServerAuthentication = "1.3.6.1.5.5.7.3.1";

    private const string AnyUsage = "2.5.29.37.0";

    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    [TestMethod]
    public void Choose_OpensTheCurrentUsersPersonalStore()
    {
        var store = new FakeClientCertificateStore();

        var chosen = AutomaticClientCertificate.Choose(store, Now);

        Assert.IsNull(chosen);
        CollectionAssert.AreEqual(new[] { (ClientCertificateStoreLocation.CurrentUser, "MY") }, store.Opened);
    }

    [TestMethod]
    public void Choose_WithAStoreThatCannotOpen_ReturnsNull()
    {
        Assert.IsNull(AutomaticClientCertificate.Choose(new FakeClientCertificateStore { Certificates = null }, Now));
    }

    [TestMethod]
    public void Choose_ReturnsTheFirstQualifyingCertificateAndDisposesTheRest()
    {
        using var serverOnly = Certificate(ServerAuthentication);
        using var first = Certificate(ClientAuthentication);
        using var second = Certificate(null);
        var store = new FakeClientCertificateStore { Certificates = [serverOnly, first, second] };

        var chosen = AutomaticClientCertificate.Choose(store, Now);

        Assert.AreSame(first, chosen);
        Assert.AreNotEqual(IntPtr.Zero, first.Handle);
        Assert.AreEqual(IntPtr.Zero, serverOnly.Handle);
        Assert.AreEqual(IntPtr.Zero, second.Handle);
    }

    [TestMethod]
    [DataRow(null, DisplayName = "No extended key usage")]
    [DataRow(ClientAuthentication, DisplayName = "Client authentication")]
    [DataRow(AnyUsage, DisplayName = "Any extended key usage")]
    public void Qualifies_WithAPrivateKeyInItsValidityAndAClientUsage_ReturnsTrue(string? usage)
    {
        using var certificate = Certificate(usage);

        Assert.IsTrue(AutomaticClientCertificate.Qualifies(certificate, Now));
    }

    [TestMethod]
    public void Qualifies_WithOnlyServerAuthentication_ReturnsFalse()
    {
        using var certificate = Certificate(ServerAuthentication);

        Assert.IsFalse(AutomaticClientCertificate.Qualifies(certificate, Now));
    }

    [TestMethod]
    public void Qualifies_WithoutAPrivateKey_ReturnsFalse()
    {
        using var withKey = Certificate(null);
        using var certificate = X509CertificateLoader.LoadCertificate(withKey.RawData);

        Assert.IsFalse(AutomaticClientCertificate.Qualifies(certificate, Now));
    }

    [TestMethod]
    [DataRow(-3, DisplayName = "Not yet valid")]
    [DataRow(3, DisplayName = "Expired")]
    public void Qualifies_OutsideItsValidity_ReturnsFalse(int daysFromNow)
    {
        using var certificate = Certificate(null);

        Assert.IsFalse(AutomaticClientCertificate.Qualifies(certificate, Now.AddDays(daysFromNow)));
    }

    // Valid from a day ago to a day ahead, with the one extended key usage given, or none.
    private static X509Certificate2 Certificate(string? usage)
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var request = new CertificateRequest("CN=Curl Auto Client", key, HashAlgorithmName.SHA256);
        if (usage is not null)
        {
            request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension([new Oid(usage)], critical: false));
        }

        return request.CreateSelfSigned(Now.AddDays(-1), Now.AddDays(1));
    }
}
