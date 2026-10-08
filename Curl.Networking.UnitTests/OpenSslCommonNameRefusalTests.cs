using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

using Curl.Testing;

namespace Curl.Networking;

[TestClass]
public sealed class OpenSslCommonNameRefusalTests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public void RefusesHostName_ForAHostNameAndIpAddressOnlyAlternativeNames_IsTrue()
    {
        using var certificate = CreateCertificate(dnsName: null, IPAddress.Loopback);

        var refuses = RefusesHostName(certificate, "localhost");

        Diagnostics.Assert("refuses host name", true, refuses);
        Assert.IsTrue(OpenSslCommonNameRefusal.RefusesHostName(certificate, "localhost"));
    }

    [TestMethod]
    public void RefusesHostName_ForAnIpLiteral_IsFalse()
    {
        using var certificate = CreateCertificate(dnsName: null, IPAddress.Loopback);

        var refuses = RefusesHostName(certificate, "127.0.0.1");

        Diagnostics.Assert("refuses host name", false, refuses);
        Assert.IsFalse(OpenSslCommonNameRefusal.RefusesHostName(certificate, "127.0.0.1"));
    }

    [TestMethod]
    public void RefusesHostName_ForAlternativeNamesWithADnsName_IsFalse()
    {
        using var certificate = CreateCertificate("localhost", IPAddress.Loopback);

        var refuses = RefusesHostName(certificate, "localhost");

        Diagnostics.Assert("refuses host name", false, refuses);
        Assert.IsFalse(OpenSslCommonNameRefusal.RefusesHostName(certificate, "localhost"));
    }

    [TestMethod]
    public void RefusesHostName_ForNoAlternativeNames_IsFalse()
    {
        using var certificate = CreateCertificate(dnsName: null, ipAddress: null);

        var refuses = RefusesHostName(certificate, "localhost");

        Diagnostics.Assert("refuses host name", false, refuses);
        Assert.IsFalse(OpenSslCommonNameRefusal.RefusesHostName(certificate, "localhost"));
    }

    private bool RefusesHostName(X509Certificate2 certificate, string hostName)
    {
        Diagnostics.Arrange("host name", hostName);

        var refuses = OpenSslCommonNameRefusal.RefusesHostName(certificate, hostName);

        Diagnostics.Act("refuses host name", refuses);
        return refuses;
    }

    private X509Certificate2 CreateCertificate(string? dnsName, IPAddress? ipAddress)
    {
        Diagnostics.Arrange("certificate", $"CN=localhost, DNS alternative name {dnsName ?? "none"}, IP alternative name {ipAddress?.ToString() ?? "none"}");
        using var key = ECDsa.Create();
        var request = new CertificateRequest("CN=localhost", key, HashAlgorithmName.SHA256);
        if (dnsName is not null || ipAddress is not null)
        {
            var names = new SubjectAlternativeNameBuilder();
            if (dnsName is not null)
            {
                names.AddDnsName(dnsName);
            }

            if (ipAddress is not null)
            {
                names.AddIpAddress(ipAddress);
            }

            request.CertificateExtensions.Add(names.Build());
        }

        return request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
    }
}
