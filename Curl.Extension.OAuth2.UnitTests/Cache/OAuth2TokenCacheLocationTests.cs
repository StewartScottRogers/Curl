namespace Curl.Extension.OAuth2.Cache;

/// <summary>Pins the default cache directory search (ADR-1034): curl's own per-user file variables, first set one wins.</summary>
[TestClass]
public sealed class OAuth2TokenCacheLocationTests
{
    [TestMethod]
    public void FindDefaultDirectory_CurlHomeSet_WinsOverHome()
    {
        var variables = new Dictionary<string, string> { ["CURL_HOME"] = "/curl/home", ["HOME"] = "/home/user" };

        Assert.AreEqual("/curl/home", OAuth2TokenCacheLocation.FindDefaultDirectory(variables.GetValueOrDefault));
    }

    [TestMethod]
    public void FindDefaultDirectory_EarlierOnesUnsetOrEmpty_TakesTheFirstSet()
    {
        var variables = new Dictionary<string, string> { ["XDG_CONFIG_HOME"] = string.Empty, ["APPDATA"] = "/app/data", ["USERPROFILE"] = "/profile" };

        Assert.AreEqual("/app/data", OAuth2TokenCacheLocation.FindDefaultDirectory(variables.GetValueOrDefault));
    }

    [TestMethod]
    public void FindDefaultDirectory_NoneSet_IsNull()
    {
        Assert.IsNull(OAuth2TokenCacheLocation.FindDefaultDirectory(_ => null));
        CollectionAssert.AreEqual(
            new[] { "CURL_HOME", "XDG_CONFIG_HOME", "HOME", "APPDATA", "USERPROFILE" }, OAuth2TokenCacheLocation.SearchedVariables.ToArray());
    }
}
