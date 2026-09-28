using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace Curl.Networking;

[TestClass]
public sealed class OpenSslCommonNameRefusalTests
{
    [TestMethod]
    public void RefusesHostName_ForAHostNameAndIpAddressOnlyAlternativeNames_IsTrue()
    {
        using var certificate = CreateCertificate(dnsName: null, IPAddress.Loopback);

        Assert.IsTrue(OpenSslCommonNameRefusal.RefusesHostName(certificate, "localhost"));
    }

    [TestMethod]
    public void RefusesHostName_ForAnIpLiteral_IsFalse()
    {
        using var certificate = CreateCertificate(dnsName: null, IPAddress.Loopback);

        Assert.IsFalse(OpenSslCommonNameRefusal.RefusesHostName(certificate, "127.0.0.1"));
    }

    [TestMethod]
    public void RefusesHostName_ForAlternativeNamesWithADnsName_IsFalse()
    {
        using var certificate = CreateCertificate("localhost", IPAddress.Loopback);

        Assert.IsFalse(OpenSslCommonNameRefusal.RefusesHostName(certificate, "localhost"));
    }

    [TestMethod]
    public void RefusesHostName_ForNoAlternativeNames_IsFalse()
    {
        using var certificate = CreateCertificate(dnsName: null, ipAddress: null);

        Assert.IsFalse(OpenSslCommonNameRefusal.RefusesHostName(certificate, "localhost"));
    }

    private static X509Certificate2 CreateCertificate(string? dnsName, IPAddress? ipAddress)
    {
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
