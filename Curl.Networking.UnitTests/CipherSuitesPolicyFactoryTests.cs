using System.Net.Security;

using Curl.Testing;

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

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public void ForThisPlatform_SupportsCipherSuitesPolicyExceptOnWindows()
    {
        var isWindows = OperatingSystem.IsWindows();
        Diagnostics.Arrange("is Windows", isWindows);
        var supports = CipherSuitesPolicyFactory.ForThisPlatform.PlatformSupportsCipherSuitesPolicy;
        Diagnostics.Act("platform supports cipher suites policy", supports);
        Diagnostics.Assert("platform supports cipher suites policy", !isWindows, supports);
        Assert.AreEqual(
            !OperatingSystem.IsWindows(), CipherSuitesPolicyFactory.ForThisPlatform.PlatformSupportsCipherSuitesPolicy);
    }

    [TestMethod]
    public void Create_WhenThePlatformDoesNotSupportCipherSuitesPolicy_BuildsNone()
    {
        Diagnostics.Arrange("platform supports cipher suites policy", false);
        Diagnostics.Arrange("suite count", Suites.Length);
        var policy = new CipherSuitesPolicyFactory(platformSupportsCipherSuitesPolicy: false).Create(Suites);
        Diagnostics.Act("policy is null", policy is null);
        Diagnostics.Assert("policy is null", true, policy is null);
        Assert.IsNull(new CipherSuitesPolicyFactory(platformSupportsCipherSuitesPolicy: false).Create(Suites));
    }

    [TestMethod]
    public void Create_WhenThePlatformSupportsCipherSuitesPolicy_ConstructsThePolicyWhichWindowsRefuses()
    {
        Diagnostics.Arrange("platform supports cipher suites policy", true);
        Diagnostics.Arrange("suite count", Suites.Length);
        var factory = new CipherSuitesPolicyFactory(platformSupportsCipherSuitesPolicy: true);

        if (OperatingSystem.IsWindows())
        {
            var exception = Assert.ThrowsExactly<PlatformNotSupportedException>(() => factory.Create(Suites));
            Diagnostics.Act("exception type", exception.GetType().Name);
            Diagnostics.Assert("exception type", nameof(PlatformNotSupportedException), exception.GetType().Name);
            return;
        }

        var allowed = factory.Create(Suites)!.AllowedCipherSuites.ToArray();
        Diagnostics.Act("allowed cipher suites", string.Join(",", allowed));
        Diagnostics.Assert("allowed cipher suites", string.Join(",", Suites), string.Join(",", allowed));
        CollectionAssert.AreEqual(Suites, factory.Create(Suites)!.AllowedCipherSuites.ToArray());
    }
}
