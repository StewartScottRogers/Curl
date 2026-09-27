using System.Net.Security;

namespace Curl.Networking;

/// <summary>
/// <see cref="CipherSuitesPolicyFactory" />: it builds no policy where the platform cannot,
/// and otherwise constructs one, which on Windows is where
/// <see cref="CipherSuitesPolicy" /> throws.
/// </summary>
[TestClass]
public sealed class CipherSuitesPolicyFactoryTests
{
    private static readonly TlsCipherSuite[] Suites =
    [
        TlsCipherSuite.TLS_ECDHE_RSA_WITH_AES_128_GCM_SHA256,
        TlsCipherSuite.TLS_AES_128_GCM_SHA256,
    ];

    [TestMethod]
    public void ForThisPlatform_SupportsCipherSuitesPolicyExceptOnWindows()
    {
        Assert.AreEqual(
            !OperatingSystem.IsWindows(), CipherSuitesPolicyFactory.ForThisPlatform.PlatformSupportsCipherSuitesPolicy);
    }

    [TestMethod]
    public void Create_WhenThePlatformDoesNotSupportCipherSuitesPolicy_BuildsNone()
    {
        Assert.IsNull(new CipherSuitesPolicyFactory(platformSupportsCipherSuitesPolicy: false).Create(Suites));
    }

    [TestMethod]
    public void Create_WhenThePlatformSupportsCipherSuitesPolicy_ConstructsThePolicyWhichWindowsRefuses()
    {
        var factory = new CipherSuitesPolicyFactory(platformSupportsCipherSuitesPolicy: true);

        if (OperatingSystem.IsWindows())
        {
            Assert.ThrowsExactly<PlatformNotSupportedException>(() => factory.Create(Suites));
            return;
        }

        CollectionAssert.AreEqual(Suites, factory.Create(Suites)!.AllowedCipherSuites.ToArray());
    }
}
