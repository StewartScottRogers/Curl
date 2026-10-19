using Curl.Api.Options.Authentication;
using Curl.Api.Options.Http;
using Curl.Cli;
using Curl.Console;
using Curl.Core;
using Curl.Testing;

namespace Curl.Engine;

/// <summary>
/// Pins how <c>--max-redirs</c>, <c>--post301</c>, <c>--post302</c>, <c>--post303</c>, <c>--follow</c> and
/// <c>--location-trusted</c> become the <see cref="RedirectPolicy" /> the redirect follower
/// applies.
/// </summary>
[TestClass]
public sealed class RedirectPolicyMappingTests
{
    private const string Url = "http://example.com/";

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public void FromCommandLine_NoRedirectOptions_UsesCurlDefaults()
    {
        RedirectPolicy policy = Map("-L", Url);
        Diagnostics.Act("max redirects", policy.MaxRedirects);
        Diagnostics.Act("keep post on 301/302/303", $"{policy.KeepPostOn301}/{policy.KeepPostOn302}/{policy.KeepPostOn303}");
        Diagnostics.Act("location trusted / disallows user in url", $"{policy.LocationTrusted}/{policy.DisallowsUserInUrl}");
        Diagnostics.Act("allowed schemes", string.Join(",", policy.AllowedSchemes.Order(StringComparer.Ordinal)));

        Diagnostics.Assert("max redirects", RedirectPolicy.DefaultMaxRedirects, policy.MaxRedirects);
        Assert.AreEqual(RedirectPolicy.DefaultMaxRedirects, policy.MaxRedirects);
        Assert.IsFalse(policy.KeepPostOn301);
        Assert.IsFalse(policy.KeepPostOn302);
        Assert.IsFalse(policy.KeepPostOn303);
        Assert.IsFalse(policy.LocationTrusted);
        Assert.IsFalse(policy.DisallowsUserInUrl);
        CollectionAssert.AreEquivalent(
            new[] { "http", "https", "ftp", "ftps" },
            policy.AllowedSchemes.ToArray());
    }

    [TestMethod]
    public void FromCommandLine_DisallowUsernameInUrl_DisallowsUserInUrl()
    {
        bool disallows = Map("-L", "--disallow-username-in-url", Url).DisallowsUserInUrl;
        Diagnostics.Act("disallows user in url", disallows);

        Diagnostics.Assert("disallows user in url", true, disallows);
        Assert.IsTrue(disallows);
    }

    [TestMethod]
    public void FromCommandLine_MaxRedirs_CopiesTheLimit()
    {
        int maxRedirects = Map("-L", "--max-redirs", "3", Url).MaxRedirects;
        Diagnostics.Act("max redirects", maxRedirects);

        Diagnostics.Assert("max redirects", 3, maxRedirects);
        Assert.AreEqual(3, maxRedirects);
    }

    [TestMethod]
    public void FromCommandLine_MaxRedirsMinusOne_MeansNoLimit()
    {
        int maxRedirects = Map("-L", "--max-redirs", "-1", Url).MaxRedirects;
        Diagnostics.Act("max redirects", maxRedirects);

        Diagnostics.Assert("max redirects", -1, maxRedirects);
        Assert.AreEqual(-1, maxRedirects);
    }

    [TestMethod]
    public void FromCommandLine_Post301_KeepsPostOn301Only()
    {
        RedirectPolicy policy = Map("-L", "--post301", Url);
        string keepPost = KeepPost(policy);
        Diagnostics.Act("keep post on 301/302/303", keepPost);

        Diagnostics.Assert("keep post on 301/302/303", "True/False/False", keepPost);
        Assert.IsTrue(policy.KeepPostOn301);
        Assert.IsFalse(policy.KeepPostOn302);
        Assert.IsFalse(policy.KeepPostOn303);
    }

    [TestMethod]
    public void FromCommandLine_Post302_KeepsPostOn302Only()
    {
        RedirectPolicy policy = Map("-L", "--post302", Url);
        string keepPost = KeepPost(policy);
        Diagnostics.Act("keep post on 301/302/303", keepPost);

        Diagnostics.Assert("keep post on 301/302/303", "False/True/False", keepPost);
        Assert.IsFalse(policy.KeepPostOn301);
        Assert.IsTrue(policy.KeepPostOn302);
        Assert.IsFalse(policy.KeepPostOn303);
    }

    [TestMethod]
    public void FromCommandLine_Post303_KeepsPostOn303Only()
    {
        RedirectPolicy policy = Map("-L", "--post303", Url);
        string keepPost = KeepPost(policy);
        Diagnostics.Act("keep post on 301/302/303", keepPost);

        Diagnostics.Assert("keep post on 301/302/303", "False/False/True", keepPost);
        Assert.IsFalse(policy.KeepPostOn301);
        Assert.IsFalse(policy.KeepPostOn302);
        Assert.IsTrue(policy.KeepPostOn303);
    }

    [TestMethod]
    public void FromCommandLine_LocationTrusted_TrustsEveryRedirectHost()
    {
        bool trusted = Map("--location-trusted", Url).LocationTrusted;
        Diagnostics.Act("location trusted", trusted);

        Diagnostics.Assert("location trusted", true, trusted);
        Assert.IsTrue(trusted);
    }

    [TestMethod]
    [DataRow(FollowLocation.FirstOnly, true)]
    [DataRow(FollowLocation.ObeyCode, false)]
    [DataRow(FollowLocation.All, false)]
    public void Of_FollowLocation_SendsCustomMethodFirstOnlyUnderFirstOnly(FollowLocation follow, bool firstOnly)
    {
        EasyHttpOptions http = new() { FollowLocation = follow };
        CommandLineOptions options = CommandLineParser.Parse([Url], _ => true).Options!;
        Diagnostics.Arrange("follow", follow);

        RedirectPolicy policy = RedirectPolicyMapping.Of(EasyOptionsFromCommandLine.TransferSettingsOf(options), http, new EasyAuthenticationOptions());

        Diagnostics.Assert("sends custom method first only", firstOnly, policy.SendsCustomMethodFirstOnly);
        Assert.AreEqual(firstOnly, policy.SendsCustomMethodFirstOnly);
        Assert.AreEqual(follow == FollowLocation.ObeyCode, policy.DropsCustomMethodOnSwitchToGet);
    }

    [TestMethod]
    public void Of_RecordBuiltDirectly_ReadsTheRedirectSettingsFromIt()
    {
        EasyHttpOptions http = new()
        {
            FollowLocation = FollowLocation.ObeyCode,
            MaxRedirects = 7,
            PostRedirect = PostRedirect.Post301 | PostRedirect.Post303,
        };
        CommandLineOptions options = CommandLineParser.Parse([Url], _ => true).Options!;
        Diagnostics.Arrange("record", http);

        RedirectPolicy policy = RedirectPolicyMapping.Of(EasyOptionsFromCommandLine.TransferSettingsOf(options), http, new EasyAuthenticationOptions());

        Diagnostics.Act("keep post on 301/302/303", KeepPost(policy));
        Diagnostics.Assert("keep post on 301/302/303", "True/False/True", KeepPost(policy));
        Assert.AreEqual("True/False/True", KeepPost(policy));
        Assert.AreEqual(7, policy.MaxRedirects);
        Assert.IsTrue(policy.DropsCustomMethodOnSwitchToGet);
    }

    [TestMethod]
    [DataRow(false, "-L")]
    [DataRow(true, "--follow")]
    [DataRow(false, "--follow", "-L")]
    [DataRow(true, "-L", "--follow")]
    public void FromCommandLine_FollowLast_DropsCustomMethodOnSwitchToGet(bool expected, params string[] arguments)
    {
        bool drops = Map([.. arguments, Url]).DropsCustomMethodOnSwitchToGet;
        Diagnostics.Act("drops custom method on switch to GET", drops);

        Diagnostics.Assert("drops custom method on switch to GET", expected, drops);
        Assert.AreEqual(expected, drops);
    }

    [TestMethod]
    public void FromCommandLine_NoProto_AllowsEverySchemeForEveryUrl()
    {
        IReadOnlyCollection<string>? schemes = Map("-L", Url).AllowedTransferSchemes;
        Diagnostics.Act("allowed transfer schemes", schemes is null ? "null" : string.Join(",", schemes));

        Diagnostics.Assert("allowed transfer schemes", "null", schemes is null ? "null" : string.Join(",", schemes));
        Assert.IsNull(schemes);
    }

    [TestMethod]
    public void FromCommandLine_ProtoAndProtoRedir_CopiesBothSchemeSets()
    {
        RedirectPolicy policy = Map("-L", "--proto", "=http,https", "--proto-redir", "=http,dict", Url);
        string transferSchemes = string.Join(",", policy.AllowedTransferSchemes!.Order(StringComparer.Ordinal));
        string redirectSchemes = string.Join(",", policy.AllowedSchemes.Order(StringComparer.Ordinal));
        Diagnostics.Act("allowed transfer schemes", transferSchemes);
        Diagnostics.Act("allowed redirect schemes", redirectSchemes);

        Diagnostics.Assert("allowed transfer schemes", "http,https", transferSchemes);
        Diagnostics.Assert("allowed redirect schemes", "dict,http", redirectSchemes);
        CollectionAssert.AreEquivalent(new[] { "http", "https" }, policy.AllowedTransferSchemes!.ToArray());
        CollectionAssert.AreEquivalent(new[] { "http", "dict" }, policy.AllowedSchemes.ToArray());
    }

    private static string KeepPost(RedirectPolicy policy) => $"{policy.KeepPostOn301}/{policy.KeepPostOn302}/{policy.KeepPostOn303}";

    private RedirectPolicy Map(params string[] arguments)
    {
        Diagnostics.Arrange("command line", "curl " + string.Join(" ", arguments));
        CommandLineParseResult parsed = CommandLineParser.Parse(arguments, _ => true);
        Diagnostics.Assert("accepted", true, parsed.IsAccepted);
        Assert.IsTrue(parsed.IsAccepted);
        return RedirectPolicyMapping.Of(EasyOptionsFromCommandLine.TransferSettingsOf(parsed.Options), EasyOptionsFromCommandLine.HttpOf(parsed.Options), EasyOptionsFromCommandLine.AuthenticationOf(parsed.Options));
    }
}
