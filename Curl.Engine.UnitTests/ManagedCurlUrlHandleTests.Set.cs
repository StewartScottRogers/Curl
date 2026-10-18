using Curl.Api.Codes;
using Curl.Api.EngineContracts.Url;

namespace Curl.Engine;

public sealed partial class ManagedCurlUrlHandleTests
{
    private const string ImapUrl = "imap://user:pw;opt@example.com:8080/p?q=1#frag";

    [TestMethod]
    [DataRow(CurlUrlPart.Url)]
    [DataRow(CurlUrlPart.Scheme)]
    [DataRow(CurlUrlPart.User)]
    [DataRow(CurlUrlPart.Fragment)]
    public void Set_PartPastMaximumInputLength_IsMalformedInputAndKeepsTheUrl(CurlUrlPart part)
    {
        using ManagedCurlUrlHandle url = Parsed(ImapUrl);

        Assert.AreEqual(CurlUrlCode.MalformedInput, url.Set(part, new string('a', 8_000_001), CurlUrlFlags.NonSupportScheme));

        Assert.AreEqual(ImapUrl, GetOk(url, CurlUrlPart.Url));
    }

    [TestMethod]
    public void Set_NullUrl_ClearsEveryPart()
    {
        using ManagedCurlUrlHandle url = Parsed(FullUrl);

        Assert.AreEqual(CurlUrlCode.Ok, url.Set(CurlUrlPart.Url, null, CurlUrlFlags.None));

        Assert.AreEqual(CurlUrlCode.NoHost, url.Get(CurlUrlPart.Url, CurlUrlFlags.None, out _));
        Assert.AreEqual(CurlUrlCode.NoScheme, url.Get(CurlUrlPart.Scheme, CurlUrlFlags.None, out _));
        Assert.AreEqual(CurlUrlCode.NoUser, url.Get(CurlUrlPart.User, CurlUrlFlags.None, out _));
    }

    [TestMethod]
    [DataRow(CurlUrlPart.Scheme, CurlUrlCode.NoScheme)]
    [DataRow(CurlUrlPart.User, CurlUrlCode.NoUser)]
    [DataRow(CurlUrlPart.Password, CurlUrlCode.NoPassword)]
    [DataRow(CurlUrlPart.Options, CurlUrlCode.NoOptions)]
    [DataRow(CurlUrlPart.Host, CurlUrlCode.NoHost)]
    [DataRow(CurlUrlPart.Port, CurlUrlCode.NoPort)]
    [DataRow(CurlUrlPart.Query, CurlUrlCode.NoQuery)]
    [DataRow(CurlUrlPart.Fragment, CurlUrlCode.NoFragment)]
    [DataRow(CurlUrlPart.ZoneId, CurlUrlCode.NoZoneId)]
    public void Set_NullPart_ClearsThatPartOnly(CurlUrlPart part, CurlUrlCode expected)
    {
        using ManagedCurlUrlHandle url = Parsed(ImapUrl);

        Assert.AreEqual(CurlUrlCode.Ok, url.Set(part, null, CurlUrlFlags.None));

        Assert.AreEqual(expected, url.Get(part, CurlUrlFlags.None, out string? value));
        Assert.IsNull(value);
        Assert.AreEqual("/p", GetOk(url, CurlUrlPart.Path));
    }

    [TestMethod]
    public void Set_NullPath_GivesTheSlashPathBack()
    {
        using ManagedCurlUrlHandle url = Parsed(FullUrl);

        Assert.AreEqual(CurlUrlCode.Ok, url.Set(CurlUrlPart.Path, null, CurlUrlFlags.None));

        Assert.AreEqual("/", GetOk(url, CurlUrlPart.Path));
        Assert.AreEqual("https://user:pw@example.com:8080/?q=1#frag", GetOk(url, CurlUrlPart.Url));
    }

    [TestMethod]
    [DataRow(11, null)]
    [DataRow(11, "x")]
    [DataRow(-1, "x")]
    public void Set_PartUpstreamDoesNotDefine_ReturnsUnknownPartAndKeepsTheUrl(int part, string? value)
    {
        using ManagedCurlUrlHandle url = Parsed(FullUrl);

        Assert.AreEqual(CurlUrlCode.UnknownPart, url.Set((CurlUrlPart)part, value, CurlUrlFlags.None));

        Assert.AreEqual(FullUrl, GetOk(url, CurlUrlPart.Url));
    }

    [TestMethod]
    public void Set_SchemeInUpperCase_StoresItLowerCasedAndRebuildsTheUrl()
    {
        using ManagedCurlUrlHandle url = Parsed("http://example.com/a");

        Assert.AreEqual(CurlUrlCode.Ok, url.Set(CurlUrlPart.Scheme, "FTP", CurlUrlFlags.None));

        Assert.AreEqual("ftp", GetOk(url, CurlUrlPart.Scheme));
        Assert.AreEqual("ftp://example.com/a", GetOk(url, CurlUrlPart.Url));
    }

    [TestMethod]
    [DataRow("", CurlUrlFlags.None, CurlUrlCode.BadScheme)]
    [DataRow("abcdefghijabcdefghijabcdefghijabcdefghijk", CurlUrlFlags.NonSupportScheme, CurlUrlCode.BadScheme)]
    [DataRow("foo", CurlUrlFlags.None, CurlUrlCode.UnsupportedScheme)]
    [DataRow("1ab", CurlUrlFlags.None, CurlUrlCode.UnsupportedScheme)]
    [DataRow("1ab", CurlUrlFlags.NonSupportScheme, CurlUrlCode.BadScheme)]
    [DataRow("a_b", CurlUrlFlags.NonSupportScheme, CurlUrlCode.BadScheme)]
    public void Set_BadScheme_ReturnsUpstreamsCodeAndKeepsTheScheme(string scheme, CurlUrlFlags flags, CurlUrlCode expected)
    {
        using ManagedCurlUrlHandle url = Parsed("http://example.com/");

        Assert.AreEqual(expected, url.Set(CurlUrlPart.Scheme, scheme, flags));

        Assert.AreEqual("http", GetOk(url, CurlUrlPart.Scheme));
    }

    [TestMethod]
    public void Set_UnsupportedSchemeUnderNonSupportScheme_IsAccepted()
    {
        using ManagedCurlUrlHandle url = Parsed("http://example.com/");

        Assert.AreEqual(CurlUrlCode.Ok, url.Set(CurlUrlPart.Scheme, "my+app-1.x", CurlUrlFlags.NonSupportScheme));

        Assert.AreEqual("my+app-1.x://example.com/", GetOk(url, CurlUrlPart.Url));
    }

    [TestMethod]
    public void Set_SchemeOnAGuessedOne_IsNoLongerGuessed()
    {
        using ManagedCurlUrlHandle url = Parsed("example.com/", CurlUrlFlags.GuessScheme);

        Assert.AreEqual(CurlUrlCode.Ok, url.Set(CurlUrlPart.Scheme, "http", CurlUrlFlags.None));

        Assert.AreEqual("http", GetOk(url, CurlUrlPart.Scheme, CurlUrlFlags.NoGuessScheme));
    }

    [TestMethod]
    [DataRow(CurlUrlPart.User)]
    [DataRow(CurlUrlPart.Password)]
    [DataRow(CurlUrlPart.Options)]
    [DataRow(CurlUrlPart.Fragment)]
    [DataRow(CurlUrlPart.ZoneId)]
    public void Set_TextPart_StoresItWithItsEscapesLowerCased(CurlUrlPart part)
    {
        using ManagedCurlUrlHandle url = Parsed("http://example.com/");

        Assert.AreEqual(CurlUrlCode.Ok, url.Set(part, "a%4A%2f%G1%", CurlUrlFlags.None));

        Assert.AreEqual("a%4a%2f%G1%", GetOk(url, part));
    }

    [TestMethod]
    [DataRow(CurlUrlPart.User)]
    [DataRow(CurlUrlPart.Password)]
    [DataRow(CurlUrlPart.Options)]
    [DataRow(CurlUrlPart.Fragment)]
    [DataRow(CurlUrlPart.ZoneId)]
    public void Set_TextPartUnderUrlEncode_EscapesAllButTheUnreservedBytes(CurlUrlPart part)
    {
        using ManagedCurlUrlHandle url = Parsed("http://example.com/");

        Assert.AreEqual(CurlUrlCode.Ok, url.Set(part, "a b/=\u00e9~", CurlUrlFlags.UrlEncode));

        Assert.AreEqual("a%20b%2F%3D%C3%A9~", GetOk(url, part));
    }

    [TestMethod]
    public void Set_UserPasswordAndOptions_AppearInTheUrl()
    {
        using ManagedCurlUrlHandle url = Parsed("imap://example.com/");

        url.Set(CurlUrlPart.User, "u", CurlUrlFlags.None);
        url.Set(CurlUrlPart.Password, "p", CurlUrlFlags.None);
        url.Set(CurlUrlPart.Options, "o", CurlUrlFlags.None);
        url.Set(CurlUrlPart.Fragment, "f", CurlUrlFlags.None);

        Assert.AreEqual("imap://u:p;o@example.com/#f", GetOk(url, CurlUrlPart.Url));
    }

    [TestMethod]
    public void Set_Host_ReplacesItAndItsZoneId()
    {
        using ManagedCurlUrlHandle url = Parsed("http://[fe80::1%25eth0]:81/x");

        Assert.AreEqual(CurlUrlCode.Ok, url.Set(CurlUrlPart.Host, "example.org", CurlUrlFlags.None));

        Assert.AreEqual("http://example.org:81/x", GetOk(url, CurlUrlPart.Url));
        Assert.AreEqual(CurlUrlCode.NoZoneId, url.Get(CurlUrlPart.ZoneId, CurlUrlFlags.None, out _));
    }

    [TestMethod]
    public void Set_HostAnIPv6AddressWithAZone_KeepsTheZoneApart()
    {
        using ManagedCurlUrlHandle url = Parsed("http://example.com/");

        Assert.AreEqual(CurlUrlCode.Ok, url.Set(CurlUrlPart.Host, "[fe80::1%25eth0]", CurlUrlFlags.None));

        Assert.AreEqual("[fe80::1]", GetOk(url, CurlUrlPart.Host));
        Assert.AreEqual("eth0", GetOk(url, CurlUrlPart.ZoneId));
    }

    [TestMethod]
    [DataRow("", CurlUrlFlags.None)]
    [DataRow("", CurlUrlFlags.UrlEncode)]
    [DataRow("a/b", CurlUrlFlags.None)]
    [DataRow("a:80", CurlUrlFlags.None)]
    [DataRow("u@h", CurlUrlFlags.None)]
    [DataRow("[::1]/x", CurlUrlFlags.None)]
    [DataRow("bad host", CurlUrlFlags.None)]
    public void Set_BadHost_ReturnsBadHostnameAndKeepsTheHost(string host, CurlUrlFlags flags)
    {
        using ManagedCurlUrlHandle url = Parsed("http://example.com/");

        Assert.AreEqual(CurlUrlCode.BadHostname, url.Set(CurlUrlPart.Host, host, flags));

        Assert.AreEqual("example.com", GetOk(url, CurlUrlPart.Host));
    }

    [TestMethod]
    public void Set_EmptyHostUnderNoAuthority_IsAccepted()
    {
        using ManagedCurlUrlHandle url = Parsed("http://example.com/");

        Assert.AreEqual(CurlUrlCode.Ok, url.Set(CurlUrlPart.Host, string.Empty, CurlUrlFlags.NoAuthority));

        Assert.AreEqual(string.Empty, GetOk(url, CurlUrlPart.Host));
    }

    [TestMethod]
    public void Set_HostUnderUrlEncode_StoresItEscaped()
    {
        using ManagedCurlUrlHandle url = Parsed("http://example.com/");

        Assert.AreEqual(CurlUrlCode.Ok, url.Set(CurlUrlPart.Host, "a b", CurlUrlFlags.UrlEncode));

        Assert.AreEqual("a%20b", GetOk(url, CurlUrlPart.Host));
    }

    [TestMethod]
    [DataRow("8080", "8080")]
    [DataRow("0080", "80")]
    [DataRow("0", "0")]
    [DataRow("65535", "65535")]
    public void Set_Port_StoresItWithoutLeadingZeros(string port, string expected)
    {
        using ManagedCurlUrlHandle url = Parsed("http://example.com/");

        Assert.AreEqual(CurlUrlCode.Ok, url.Set(CurlUrlPart.Port, port, CurlUrlFlags.None));

        Assert.AreEqual(expected, GetOk(url, CurlUrlPart.Port));
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("x1")]
    [DataRow("1x")]
    [DataRow("+1")]
    [DataRow("65536")]
    [DataRow("99999999999")]
    public void Set_BadPort_ReturnsBadPortNumberAndKeepsThePort(string port)
    {
        using ManagedCurlUrlHandle url = Parsed("http://example.com:81/");

        Assert.AreEqual(CurlUrlCode.BadPortNumber, url.Set(CurlUrlPart.Port, port, CurlUrlFlags.None));

        Assert.AreEqual("81", GetOk(url, CurlUrlPart.Port));
    }

    [TestMethod]
    [DataRow("a/b", CurlUrlFlags.None, "/a/b")]
    [DataRow("/a%2Fb", CurlUrlFlags.None, "/a%2fb")]
    [DataRow("", CurlUrlFlags.None, "/")]
    [DataRow("/x y/\u00e9", CurlUrlFlags.UrlEncode, "/x%20y/%C3%A9")]
    [DataRow("x y", CurlUrlFlags.UrlEncode, "/x%20y")]
    public void Set_Path_StartsItWithASlash(string path, CurlUrlFlags flags, string expected)
    {
        using ManagedCurlUrlHandle url = Parsed("http://example.com/old");

        Assert.AreEqual(CurlUrlCode.Ok, url.Set(CurlUrlPart.Path, path, flags));

        Assert.AreEqual(expected, GetOk(url, CurlUrlPart.Path));
    }

    [TestMethod]
    [DataRow("a=1", CurlUrlFlags.None, "a=1")]
    [DataRow("a b=c=d", CurlUrlFlags.UrlEncode, "a+b%3Dc%3Dd")]
    [DataRow("b=2", CurlUrlFlags.AppendQuery, "q=1&b=2")]
    [DataRow("x=a b=c", CurlUrlFlags.AppendQuery | CurlUrlFlags.UrlEncode, "q=1&x=a+b%3Dc")]
    public void Set_Query_ReplacesOrAppends(string query, CurlUrlFlags flags, string expected)
    {
        using ManagedCurlUrlHandle url = Parsed("http://example.com/?q=1");

        Assert.AreEqual(CurlUrlCode.Ok, url.Set(CurlUrlPart.Query, query, flags));

        Assert.AreEqual(expected, GetOk(url, CurlUrlPart.Query));
    }

    [TestMethod]
    [DataRow("http://example.com/?q=1&", "q=1&b=2")]
    [DataRow("http://example.com/?", "b=2")]
    [DataRow("http://example.com/", "b=2")]
    public void Set_AppendQuery_AddsOneAmpersandOnlyWhenNeeded(string text, string expected)
    {
        using ManagedCurlUrlHandle url = Parsed(text);

        Assert.AreEqual(CurlUrlCode.Ok, url.Set(CurlUrlPart.Query, "b=2", CurlUrlFlags.AppendQuery));

        Assert.AreEqual(expected, GetOk(url, CurlUrlPart.Query));
    }

    [TestMethod]
    public void Set_EmptyQuery_IsAnEmptyQueryOnlyUnderGetEmpty()
    {
        using ManagedCurlUrlHandle url = Parsed("http://example.com/");

        Assert.AreEqual(CurlUrlCode.Ok, url.Set(CurlUrlPart.Query, string.Empty, CurlUrlFlags.None));

        Assert.AreEqual(CurlUrlCode.NoQuery, url.Get(CurlUrlPart.Query, CurlUrlFlags.None, out _));
        Assert.AreEqual("http://example.com/?", GetOk(url, CurlUrlPart.Url, CurlUrlFlags.GetEmpty));
    }

    [TestMethod]
    public void Set_EmptyUrl_ReturnsMalformedInput()
    {
        using ManagedCurlUrlHandle url = Parsed(FullUrl);

        Assert.AreEqual(CurlUrlCode.MalformedInput, url.Set(CurlUrlPart.Url, string.Empty, CurlUrlFlags.None));
        Assert.AreEqual(CurlUrlCode.MalformedInput, url.Set(CurlUrlPart.Url, "http://h/" + new string('a', 8_000_000), CurlUrlFlags.None));

        Assert.AreEqual(FullUrl, GetOk(url, CurlUrlPart.Url));
    }

    [TestMethod]
    public void Set_UrlWithAnUnsupportedScheme_ReturnsUnsupportedSchemeUnlessNonSupportScheme()
    {
        using ManagedCurlUrlHandle url = new();

        Assert.AreEqual(CurlUrlCode.UnsupportedScheme, url.Set(CurlUrlPart.Url, "foo://example.com/x", CurlUrlFlags.None));
        Assert.AreEqual(CurlUrlCode.Ok, url.Set(CurlUrlPart.Url, "foo://example.com/x", CurlUrlFlags.NonSupportScheme));

        Assert.AreEqual("foo", GetOk(url, CurlUrlPart.Scheme));
    }

    [TestMethod]
    public void Set_UrlWithSpacesUnderAllowSpace_KeepsTheSpaces()
    {
        using ManagedCurlUrlHandle url = new();

        Assert.AreEqual(CurlUrlCode.MalformedInput, url.Set(CurlUrlPart.Url, "http://h/a b", CurlUrlFlags.None));
        Assert.AreEqual(CurlUrlCode.Ok, url.Set(CurlUrlPart.Url, "http://u s@h/a b?c d#e f", CurlUrlFlags.AllowSpace));

        Assert.AreEqual("u s", GetOk(url, CurlUrlPart.User));
        Assert.AreEqual("/a b", GetOk(url, CurlUrlPart.Path));
        Assert.AreEqual("c d", GetOk(url, CurlUrlPart.Query));
        Assert.AreEqual("e f", GetOk(url, CurlUrlPart.Fragment));
    }

    [TestMethod]
    public void Set_UrlWithSpacesAndAnEscapedSpaceUnderAllowSpace_StoresBothEscaped()
    {
        using ManagedCurlUrlHandle url = new();

        Assert.AreEqual(CurlUrlCode.Ok, url.Set(CurlUrlPart.Url, "http://h/a b%20", CurlUrlFlags.AllowSpace));

        Assert.AreEqual("/a%20b%20", GetOk(url, CurlUrlPart.Path));
    }

    [TestMethod]
    [DataRow("c", "http://h/a/c")]
    [DataRow("../c d", "http://h/c%20d")]
    [DataRow("/x", "http://h/x")]
    [DataRow("//o/p", "http://o/p")]
    [DataRow("?n", "http://h/a/b?n")]
    [DataRow("#g", "http://h/a/b?q#g")]
    public void Set_RelativeUrl_ResolvesItAgainstTheUrlHeld(string relative, string expected)
    {
        using ManagedCurlUrlHandle url = Parsed("http://h/a/b?q#f");

        Assert.AreEqual(CurlUrlCode.Ok, url.Set(CurlUrlPart.Url, relative, CurlUrlFlags.None));

        Assert.AreEqual(expected, GetOk(url, CurlUrlPart.Url));
    }

    [TestMethod]
    public void Set_RelativeUrlOnAGuessedSchemeUnderNoGuessScheme_ResolvesWithTheGuessedScheme()
    {
        using ManagedCurlUrlHandle url = Parsed("example.com/a/b", CurlUrlFlags.GuessScheme);

        Assert.AreEqual(CurlUrlCode.Ok, url.Set(CurlUrlPart.Url, "c", CurlUrlFlags.NoGuessScheme));

        Assert.AreEqual("http://example.com/a/c", GetOk(url, CurlUrlPart.Url));
    }

    [TestMethod]
    public void Set_RelativeUrlOnAnEmptyHandle_ParsesItWhole()
    {
        using ManagedCurlUrlHandle url = new();

        Assert.AreEqual(CurlUrlCode.BadScheme, url.Set(CurlUrlPart.Url, "c", CurlUrlFlags.None));
        Assert.AreEqual(CurlUrlCode.Ok, url.Set(CurlUrlPart.Url, "c", CurlUrlFlags.GuessScheme));

        Assert.AreEqual("http://c/", GetOk(url, CurlUrlPart.Url));
    }

    [TestMethod]
    public void Duplicate_Url_IsACopyThatChangesApart()
    {
        using ManagedCurlUrlHandle url = Parsed(FullUrl);
        using ICurlUrlHandle copy = url.Duplicate();

        Assert.AreEqual(CurlUrlCode.Ok, copy.Set(CurlUrlPart.Host, "other.org", CurlUrlFlags.None));

        Assert.AreEqual(CurlUrlCode.Ok, copy.Get(CurlUrlPart.Url, CurlUrlFlags.None, out string? copied));
        Assert.AreEqual("https://user:pw@other.org:8080/p/a?q=1#frag", copied);
        Assert.AreEqual(FullUrl, GetOk(url, CurlUrlPart.Url));
    }
}
