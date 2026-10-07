using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

using Curl.Networking.Fakes;
using Curl.Testing;

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

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public void Choose_OpensTheCurrentUsersPersonalStore()
    {
        var store = new FakeClientCertificateStore();
        Diagnostics.Arrange("store certificates", "default fake store");

        var chosen = AutomaticClientCertificate.Choose(store, Now);
        Diagnostics.Act("chosen is null", chosen is null);
        Diagnostics.Act("opened stores", string.Join(", ", store.Opened.Select(opened => $"{opened.Item1}/{opened.Item2}")));

        Diagnostics.Assert("chosen is null", true, chosen is null);
        Assert.IsNull(chosen);
        var expected = new[] { (ClientCertificateStoreLocation.CurrentUser, "MY") };
        Diagnostics.Assert("opened count", expected.Length, store.Opened.Count);
        CollectionAssert.AreEqual(expected, store.Opened);
    }

    [TestMethod]
    public void Choose_WithAStoreThatCannotOpen_ReturnsNull()
    {
        Diagnostics.Arrange("store certificates", "null (cannot open)");

        var chosen = AutomaticClientCertificate.Choose(new FakeClientCertificateStore { Certificates = null }, Now);
        Diagnostics.Act("chosen is null", chosen is null);

        Diagnostics.Assert("chosen is null", true, chosen is null);
        Assert.IsNull(chosen);
    }

    [TestMethod]
    public void Choose_ReturnsTheFirstQualifyingCertificateAndDisposesTheRest()
    {
        using var serverOnly = Certificate(ServerAuthentication);
        using var first = Certificate(ClientAuthentication);
        using var second = Certificate(null);
        var store = new FakeClientCertificateStore { Certificates = [serverOnly, first, second] };
        Diagnostics.Arrange("server-only subject", serverOnly.Subject);
        Diagnostics.Arrange("first subject", first.Subject);
        Diagnostics.Arrange("second subject", second.Subject);

        var chosen = AutomaticClientCertificate.Choose(store, Now);
        Diagnostics.Act("chosen is first", ReferenceEquals(first, chosen));
        Diagnostics.Act("handles nonzero (first, serverOnly, second)", $"{first.Handle != IntPtr.Zero}, {serverOnly.Handle != IntPtr.Zero}, {second.Handle != IntPtr.Zero}");

        Diagnostics.Assert("chosen is first", true, ReferenceEquals(first, chosen));
        Assert.AreSame(first, chosen);
        Diagnostics.Assert("first handle nonzero", true, first.Handle != IntPtr.Zero);
        Assert.AreNotEqual(IntPtr.Zero, first.Handle);
        Diagnostics.Assert("server-only handle is zero", true, serverOnly.Handle == IntPtr.Zero);
        Assert.AreEqual(IntPtr.Zero, serverOnly.Handle);
        Diagnostics.Assert("second handle is zero", true, second.Handle == IntPtr.Zero);
        Assert.AreEqual(IntPtr.Zero, second.Handle);
    }

    [TestMethod]
    [DataRow(null, DisplayName = "No extended key usage")]
    [DataRow(ClientAuthentication, DisplayName = "Client authentication")]
    [DataRow(AnyUsage, DisplayName = "Any extended key usage")]
    public void Qualifies_WithAPrivateKeyInItsValidityAndAClientUsage_ReturnsTrue(string? usage)
    {
        using var certificate = Certificate(usage);
        Diagnostics.Arrange("usage", usage ?? "(none)");

        var qualifies = AutomaticClientCertificate.Qualifies(certificate, Now);
        Diagnostics.Act("qualifies", qualifies);

        Diagnostics.Assert("qualifies", true, qualifies);
        Assert.IsTrue(qualifies);
    }

    [TestMethod]
    public void Qualifies_WithOnlyServerAuthentication_ReturnsFalse()
    {
        using var certificate = Certificate(ServerAuthentication);
        Diagnostics.Arrange("usage", ServerAuthentication);

        var qualifies = AutomaticClientCertificate.Qualifies(certificate, Now);
        Diagnostics.Act("qualifies", qualifies);

        Diagnostics.Assert("qualifies", false, qualifies);
        Assert.IsFalse(qualifies);
    }

    [TestMethod]
    public void Qualifies_WithoutAPrivateKey_ReturnsFalse()
    {
        using var withKey = Certificate(null);
        using var certificate = X509CertificateLoader.LoadCertificate(withKey.RawData);
        Diagnostics.Arrange("has private key", certificate.HasPrivateKey);
        Diagnostics.Arrange("subject", certificate.Subject);

        var qualifies = AutomaticClientCertificate.Qualifies(certificate, Now);
        Diagnostics.Act("qualifies", qualifies);

        Diagnostics.Assert("qualifies", false, qualifies);
        Assert.IsFalse(qualifies);
    }

    [TestMethod]
    [DataRow(-3, DisplayName = "Not yet valid")]
    [DataRow(3, DisplayName = "Expired")]
    public void Qualifies_OutsideItsValidity_ReturnsFalse(int daysFromNow)
    {
        using var certificate = Certificate(null);
        Diagnostics.Arrange("days from now", daysFromNow);

        var qualifies = AutomaticClientCertificate.Qualifies(certificate, Now.AddDays(daysFromNow));
        Diagnostics.Act("qualifies", qualifies);

        Diagnostics.Assert("qualifies", false, qualifies);
        Assert.IsFalse(qualifies);
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
