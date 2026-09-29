using Curl.Cli;
using Curl.Core;

namespace Curl.Console;

/// <summary>
/// Pins how <c>--max-redirs</c>, <c>--post301</c>, <c>--post302</c>, <c>--post303</c> and
/// <c>--location-trusted</c> become the <see cref="RedirectPolicy" /> the redirect follower
/// applies.
/// </summary>
[TestClass]
public sealed class RedirectPolicyMappingTests
{
    private const string Url = "http://example.com/";

    [TestMethod]
    public void FromCommandLine_NoRedirectOptions_UsesCurlDefaults()
    {
        RedirectPolicy policy = Map("-L", Url);

        Assert.AreEqual(RedirectPolicy.DefaultMaxRedirects, policy.MaxRedirects);
        Assert.IsFalse(policy.KeepPostOn301);
        Assert.IsFalse(policy.KeepPostOn302);
        Assert.IsFalse(policy.KeepPostOn303);
        Assert.IsFalse(policy.LocationTrusted);
        CollectionAssert.AreEquivalent(
            new[] { "http", "https", "ftp", "ftps" },
            policy.AllowedSchemes.ToArray());
    }

    [TestMethod]
    public void FromCommandLine_MaxRedirs_CopiesTheLimit()
    {
        Assert.AreEqual(3, Map("-L", "--max-redirs", "3", Url).MaxRedirects);
    }

    [TestMethod]
    public void FromCommandLine_MaxRedirsMinusOne_MeansNoLimit()
    {
        Assert.AreEqual(-1, Map("-L", "--max-redirs", "-1", Url).MaxRedirects);
    }

    [TestMethod]
    public void FromCommandLine_Post301_KeepsPostOn301Only()
    {
        RedirectPolicy policy = Map("-L", "--post301", Url);

        Assert.IsTrue(policy.KeepPostOn301);
        Assert.IsFalse(policy.KeepPostOn302);
        Assert.IsFalse(policy.KeepPostOn303);
    }

    [TestMethod]
    public void FromCommandLine_Post302_KeepsPostOn302Only()
    {
        RedirectPolicy policy = Map("-L", "--post302", Url);

        Assert.IsFalse(policy.KeepPostOn301);
        Assert.IsTrue(policy.KeepPostOn302);
        Assert.IsFalse(policy.KeepPostOn303);
    }

    [TestMethod]
    public void FromCommandLine_Post303_KeepsPostOn303Only()
    {
        RedirectPolicy policy = Map("-L", "--post303", Url);

        Assert.IsFalse(policy.KeepPostOn301);
        Assert.IsFalse(policy.KeepPostOn302);
        Assert.IsTrue(policy.KeepPostOn303);
    }

    [TestMethod]
    public void FromCommandLine_LocationTrusted_TrustsEveryRedirectHost()
    {
        Assert.IsTrue(Map("--location-trusted", Url).LocationTrusted);
    }

    [TestMethod]
    public void FromCommandLine_NoProto_AllowsEverySchemeForEveryUrl()
    {
        Assert.IsNull(Map("-L", Url).AllowedTransferSchemes);
    }

    [TestMethod]
    public void FromCommandLine_ProtoAndProtoRedir_CopiesBothSchemeSets()
    {
        RedirectPolicy policy = Map("-L", "--proto", "=http,https", "--proto-redir", "=http,dict", Url);

        CollectionAssert.AreEquivalent(new[] { "http", "https" }, policy.AllowedTransferSchemes!.ToArray());
        CollectionAssert.AreEquivalent(new[] { "http", "dict" }, policy.AllowedSchemes.ToArray());
    }

    private static RedirectPolicy Map(params string[] arguments)
    {
        CommandLineParseResult parsed = CommandLineParser.Parse(arguments, _ => true);
        Assert.IsTrue(parsed.IsAccepted);
        return RedirectPolicyMapping.FromCommandLine(parsed.Options);
    }
}
