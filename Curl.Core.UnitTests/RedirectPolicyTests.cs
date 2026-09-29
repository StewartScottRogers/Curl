namespace Curl.Core;

/// <summary>
/// Pins <see cref="RedirectPolicy" />'s defaults to curl 8.21.0's: 50 redirects, no
/// <c>--post30x</c>, no <c>--location-trusted</c>, and redirects to http, https, ftp and
/// ftps only.
/// </summary>
[TestClass]
public sealed class RedirectPolicyTests
{
    [TestMethod]
    public void Constructor_NoOptionGiven_HasCurlDefaults()
    {
        RedirectPolicy policy = new();

        Assert.AreEqual(50, policy.MaxRedirects);
        Assert.AreEqual(RedirectPolicy.DefaultMaxRedirects, policy.MaxRedirects);
        Assert.IsFalse(policy.KeepPostOn301);
        Assert.IsFalse(policy.KeepPostOn302);
        Assert.IsFalse(policy.KeepPostOn303);
        Assert.IsFalse(policy.LocationTrusted);
        Assert.IsFalse(policy.DropsCustomMethodOnSwitchToGet);
        Assert.IsFalse(policy.DisallowsUserInUrl);
        CollectionAssert.AreEquivalent(
            new[] { "http", "https", "ftp", "ftps" },
            policy.AllowedSchemes.ToArray());
    }

    [TestMethod]
    public void With_OneOptionChanged_KeepsTheOthers()
    {
        RedirectPolicy policy = new() { KeepPostOn302 = true };

        RedirectPolicy copy = policy with { MaxRedirects = 3 };

        Assert.AreEqual(3, copy.MaxRedirects);
        Assert.IsTrue(copy.KeepPostOn302);
        Assert.AreSame(policy.AllowedSchemes, copy.AllowedSchemes);
    }
}
