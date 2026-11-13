using System.Net;
using System.Text;
using Curl.Api.Options.Authentication;
using Curl.Core;
using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Engine.UnitTests;

/// <summary>
/// Pins <see cref="TransferCredentialLookup.ForRedirectHops" /> for a transfer with a <c>-u</c> user name and a
/// netrc option: a hop to another host sends that host's own netrc entry, as curl 8.22.0 does (STD-040, BL-5035),
/// while a hop <c>-u</c> goes to keeps it.
/// </summary>
[TestClass]
public sealed class TransferCredentialLookupRedirectNetrcTests
{
    private const string NetrcPath = "netrc";

    private static readonly CurlUrl FirstUrl = CurlUrl.Parse("http://a.test:42001/r");

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public void ForRedirectHops_UserOptionAndNetrcOptional_SendsTheOtherHostsOwnEntry()
    {
        EasyAuthenticationOptions options = UserOptionWithNetrc("user:pass");
        Diagnostics.Arrange("options", options);

        NetworkCredential? hop = Lookup().ForRedirectHops(options, FirstUrl)!(CurlUrl.Parse("http://b.test:42002/echo"));
        Diagnostics.Act("hop", $"{hop?.UserName}:{hop?.Password}");

        Assert.AreEqual("buser", hop!.UserName);
        Assert.AreEqual("bpass", hop.Password);
    }

    [TestMethod]
    public void ForRedirectHops_UserOptionAndNetrcOptional_KeepsTheUserOptionForTheSameOrigin()
    {
        EasyAuthenticationOptions options = UserOptionWithNetrc("user:pass");
        Diagnostics.Arrange("options", options);

        NetworkCredential? hop = Lookup().ForRedirectHops(options, FirstUrl)!(CurlUrl.Parse("http://A.TEST:42001/next"));
        Diagnostics.Act("hop", $"{hop?.UserName}:{hop?.Password}");

        Assert.AreEqual("user", hop!.UserName);
        Assert.AreEqual("pass", hop.Password);
    }

    [TestMethod]
    [DataRow("http://a.test:42003/next", "nuser")]
    [DataRow("https://a.test:42001/next", "nuser")]
    public void ForRedirectHops_UserOptionAndNetrcOptional_LooksUpAnotherPortOrSchemeOfTheSameHost(string hopUrl, string expectedUser)
    {
        EasyAuthenticationOptions options = UserOptionWithNetrc("user:pass");
        Diagnostics.Arrange("hop URL", hopUrl);

        NetworkCredential? hop = Lookup().ForRedirectHops(options, FirstUrl)!(CurlUrl.Parse(hopUrl));
        Diagnostics.Act("hop user", hop?.UserName);

        Assert.AreEqual(expectedUser, hop!.UserName);
    }

    [TestMethod]
    public void ForRedirectHops_UserOptionAndNetrcOptional_SendsNothingToAHostWithNoEntry()
    {
        EasyAuthenticationOptions options = UserOptionWithNetrc("user:pass");
        Diagnostics.Arrange("options", options);

        NetworkCredential? hop = Lookup().ForRedirectHops(options, FirstUrl)!(CurlUrl.Parse("http://c.test/echo"));
        Diagnostics.Act("hop", hop);

        Assert.IsNull(hop);
    }

    [TestMethod]
    public void ForRedirectHops_UserOptionAndNetrcOptional_SendsTheHopUrlsOwnCredentialsToAHostWithNoEntry()
    {
        EasyAuthenticationOptions options = UserOptionWithNetrc("user:pass");
        Diagnostics.Arrange("options", options);

        NetworkCredential? hop = Lookup().ForRedirectHops(options, FirstUrl)!(CurlUrl.Parse("http://c:d@c.test/echo"));
        Diagnostics.Act("hop", $"{hop?.UserName}:{hop?.Password}");

        Assert.AreEqual("c", hop!.UserName);
        Assert.AreEqual("d", hop.Password);
    }

    [TestMethod]
    public void ForRedirectHops_UserOptionNetrcAndLocationTrusted_KeepsTheUserOptionOnAnotherHost()
    {
        EasyAuthenticationOptions options = UserOptionWithNetrc("user:pass") with { UnrestrictedAuthentication = true };
        Diagnostics.Arrange("options", options);

        NetworkCredential? hop = Lookup().ForRedirectHops(options, FirstUrl)!(CurlUrl.Parse("http://b.test:42002/echo"));
        Diagnostics.Act("hop", $"{hop?.UserName}:{hop?.Password}");

        Assert.AreEqual("user", hop!.UserName);
        Assert.AreEqual("pass", hop.Password);
    }

    [TestMethod]
    [DataRow("user", null, null, "user", "")]
    [DataRow("user:pa:ss", null, null, "user", "pa:ss")]
    [DataRow("user:pass", "name", "secret", "name", "secret")]
    [DataRow(null, "name", null, "name", "")]
    public void ForRedirectHops_UserOptionOnTheSameOrigin_SendsItSplitAsTheContextCarriesIt(
        string? userPassword, string? userName, string? password, string expectedUser, string expectedPassword)
    {
        EasyAuthenticationOptions options = new()
        {
            Netrc = NetrcUsage.Required,
            NetrcFile = NetrcPath,
            UserPassword = userPassword,
            UserName = userName,
            Password = password,
        };
        Diagnostics.Arrange("options", options);

        NetworkCredential? hop = Lookup().ForRedirectHops(options, FirstUrl)!(CurlUrl.Parse("http://a.test:42001/next"));
        Diagnostics.Act("hop", $"{hop?.UserName}:{hop?.Password}");

        Assert.AreEqual(expectedUser, hop!.UserName);
        Assert.AreEqual(expectedPassword, hop.Password);
    }

    [TestMethod]
    public void ForRedirectHops_UserOptionAndNoFirstUrl_LeavesTheHopsToTheFollower()
    {
        EasyAuthenticationOptions options = UserOptionWithNetrc("user:pass");
        Diagnostics.Arrange("options", options);

        HopCredentialSelector? selector = Lookup().ForRedirectHops(options);
        Diagnostics.Act("selector", selector);

        Assert.IsNull(selector);
    }

    [TestMethod]
    public void ForRedirectHops_UserOptionWithoutNetrc_LeavesTheHopsToTheFollower()
    {
        EasyAuthenticationOptions options = new() { UserPassword = "user:pass" };
        Diagnostics.Arrange("options", options);

        HopCredentialSelector? selector = Lookup().ForRedirectHops(options, FirstUrl);
        Diagnostics.Act("selector", selector);

        Assert.IsNull(selector);
    }

    [TestMethod]
    public void ForRedirectHops_NetrcWithoutUserOption_LooksEachHopUpAsBefore()
    {
        EasyAuthenticationOptions options = new() { Netrc = NetrcUsage.Optional, NetrcFile = NetrcPath };
        Diagnostics.Arrange("options", options);

        HopCredentialSelector selector = Lookup().ForRedirectHops(options, FirstUrl)!;
        NetworkCredential? sameHost = selector(CurlUrl.Parse("http://a.test:42001/next"));
        NetworkCredential? otherHost = selector(CurlUrl.Parse("http://b.test:42002/echo"));
        Diagnostics.Act("same / other", $"{sameHost?.UserName} / {otherHost?.UserName}");

        Assert.AreEqual("nuser", sameHost!.UserName);
        Assert.AreEqual("buser", otherHost!.UserName);
    }

    private static EasyAuthenticationOptions UserOptionWithNetrc(string userPassword) =>
        new() { Netrc = NetrcUsage.Optional, NetrcFile = NetrcPath, UserPassword = userPassword };

    private static TransferCredentialLookup Lookup()
    {
        byte[] netrc = Encoding.UTF8.GetBytes("machine a.test login nuser password npass\nmachine b.test login buser password bpass\n");
        return new(path => path == NetrcPath ? netrc : null, _ => null, runsOnWindows: false);
    }
}
