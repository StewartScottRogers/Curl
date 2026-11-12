using System.Net;
using Curl.Api.Options.Authentication;
using Curl.Authentication;
using Curl.Protocol.Abstractions;

namespace Curl.Engine;

/// <summary>
/// BL-4915: curl 8.22.0 takes a URL's user name and, when the URL has no password, <c>-u</c>'s
/// password: <c>curl http://user@127.0.0.1:18500/ -u :secret</c> sends
/// <c>Authorization: Basic dXNlcjpzZWNyZXQ=</c> and the IMAP one <c>A002 LOGIN user secret</c>
/// (the outside fitness review of 2026-11-11, finding STD-012).
/// </summary>
[TestClass]
public sealed class UrlUserWithOptionPasswordTests
{
    [TestMethod]
    public void TryLookUp_UrlUserAndOptionPassword_SendsTheUrlUserWithTheOptionPassword()
    {
        NetworkCredential credentials = LookUp(new EasyAuthenticationOptions { UserPassword = ":secret" }, "http://user@h/");

        Assert.AreEqual("user", credentials.UserName);
        Assert.AreEqual("secret", credentials.Password);
    }

    [TestMethod]
    public void TryLookUp_UrlUserAndPasswordOption_SendsTheUrlUserWithThePasswordOption()
    {
        NetworkCredential credentials = LookUp(new EasyAuthenticationOptions { Password = "secret" }, "imap://user@h/INBOX");

        Assert.AreEqual("user:secret", credentials.UserName + ":" + credentials.Password);
    }

    [TestMethod]
    public void TryLookUp_UrlPasswordAndOptionPassword_SendsTheUrlPassword()
    {
        NetworkCredential credentials = LookUp(new EasyAuthenticationOptions { UserPassword = ":secret" }, "http://user:own@h/");

        Assert.AreEqual("user:own", credentials.UserName + ":" + credentials.Password);
    }

    [TestMethod]
    public void TryLookUp_UrlUserAndOptionWithoutColon_SendsAnEmptyPassword()
    {
        NetworkCredential credentials = LookUp(new EasyAuthenticationOptions { UserPassword = string.Empty }, "http://user@h/");

        Assert.AreEqual("user:", credentials.UserName + ":" + credentials.Password);
    }

    [TestMethod]
    public void TryLookUp_UrlUserWithNoNetrcEntryAndOptionPassword_SendsTheOptionPassword()
    {
        TransferCredentialLookup lookup = new(_ => "machine other login o password p\n"u8.ToArray(), _ => null, runsOnWindows: false);
        EasyAuthenticationOptions options = new() { UserPassword = ":secret", Netrc = NetrcUsage.Required, NetrcFile = "netrc" };

        Assert.IsTrue(lookup.TryLookUp(options, CurlUrl.Parse("http://user@h/"), out NetworkCredential? credentials, out _));

        Assert.AreEqual("user:secret", credentials!.UserName + ":" + credentials.Password);
    }

    private static NetworkCredential LookUp(EasyAuthenticationOptions options, string url)
    {
        TransferCredentialLookup lookup = new(_ => null, _ => null, runsOnWindows: false);
        Assert.IsTrue(lookup.TryLookUp(options, CurlUrl.Parse(url), out NetworkCredential? credentials, out _));
        return credentials!;
    }
}
