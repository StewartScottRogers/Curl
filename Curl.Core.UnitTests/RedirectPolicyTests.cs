using Curl.Testing;

namespace Curl.Core;

/// <summary>
/// Pins <see cref="RedirectPolicy" />'s defaults to curl 8.21.0's: 50 redirects, no
/// <c>--post30x</c>, no <c>--location-trusted</c>, and redirects to http, https, ftp and
/// ftps only.
/// </summary>
[TestClass]
public sealed class RedirectPolicyTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void Constructor_NoOptionGiven_HasCurlDefaults()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("options", "(none)");

        RedirectPolicy policy = new();

        diagnostics.Act("policy", policy);
        diagnostics.Act("allowed schemes", string.Join(",", policy.AllowedSchemes.Order(StringComparer.Ordinal)));
        diagnostics.Assert("max redirects", 50, policy.MaxRedirects);
        Assert.AreEqual(50, policy.MaxRedirects);
        Assert.AreEqual(RedirectPolicy.DefaultMaxRedirects, policy.MaxRedirects);
        diagnostics.Assert("keep POST on 301/302/303", "False/False/False", $"{policy.KeepPostOn301}/{policy.KeepPostOn302}/{policy.KeepPostOn303}");
        Assert.IsFalse(policy.KeepPostOn301);
        Assert.IsFalse(policy.KeepPostOn302);
        Assert.IsFalse(policy.KeepPostOn303);
        diagnostics.Assert("location trusted", false, policy.LocationTrusted);
        Assert.IsFalse(policy.LocationTrusted);
        diagnostics.Assert("drops custom method on switch to GET", false, policy.DropsCustomMethodOnSwitchToGet);
        Assert.IsFalse(policy.DropsCustomMethodOnSwitchToGet);
        diagnostics.Assert("disallows user in URL", false, policy.DisallowsUserInUrl);
        Assert.IsFalse(policy.DisallowsUserInUrl);
        diagnostics.Assert("allowed schemes", "ftp,ftps,http,https", string.Join(",", policy.AllowedSchemes.Order(StringComparer.Ordinal)));
        CollectionAssert.AreEquivalent(
            new[] { "http", "https", "ftp", "ftps" },
            policy.AllowedSchemes.ToArray());
    }

    [TestMethod]
    public void With_OneOptionChanged_KeepsTheOthers()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        RedirectPolicy policy = new() { KeepPostOn302 = true };
        diagnostics.Arrange("policy", "KeepPostOn302 = True");
        diagnostics.Arrange("changed", "MaxRedirects = 3");

        RedirectPolicy copy = policy with { MaxRedirects = 3 };

        diagnostics.Act("copy max redirects", copy.MaxRedirects);
        diagnostics.Act("copy keep POST on 302", copy.KeepPostOn302);
        diagnostics.Assert("max redirects", 3, copy.MaxRedirects);
        Assert.AreEqual(3, copy.MaxRedirects);
        diagnostics.Assert("keep POST on 302", true, copy.KeepPostOn302);
        Assert.IsTrue(copy.KeepPostOn302);
        diagnostics.Assert("allowed schemes same instance", true, ReferenceEquals(policy.AllowedSchemes, copy.AllowedSchemes));
        Assert.AreSame(policy.AllowedSchemes, copy.AllowedSchemes);
    }
}
