using Curl.Api.Codes;
using Curl.Api.EngineContracts.Url;

namespace Curl.Engine;

[TestClass]
public sealed partial class ManagedCurlUrlHandleTests
{
    private const string FullUrl = "https://user:pw@example.com:8080/p/a?q=1#frag";

    [TestMethod]
    [DataRow(CurlUrlPart.Url, FullUrl)]
    [DataRow(CurlUrlPart.Scheme, "https")]
    [DataRow(CurlUrlPart.User, "user")]
    [DataRow(CurlUrlPart.Password, "pw")]
    [DataRow(CurlUrlPart.Host, "example.com")]
    [DataRow(CurlUrlPart.Port, "8080")]
    [DataRow(CurlUrlPart.Path, "/p/a")]
    [DataRow(CurlUrlPart.Query, "q=1")]
    [DataRow(CurlUrlPart.Fragment, "frag")]
    public void Get_EveryPartOfAFullUrl_ReturnsThePart(CurlUrlPart part, string expected)
    {
        using ManagedCurlUrlHandle url = Parsed(FullUrl);

        Assert.AreEqual(CurlUrlCode.Ok, url.Get(part, CurlUrlFlags.None, out string? value));
        Assert.AreEqual(expected, value);
    }

    [TestMethod]
    [DataRow(CurlUrlPart.User, CurlUrlCode.NoUser)]
    [DataRow(CurlUrlPart.Password, CurlUrlCode.NoPassword)]
    [DataRow(CurlUrlPart.Options, CurlUrlCode.NoOptions)]
    [DataRow(CurlUrlPart.Port, CurlUrlCode.NoPort)]
    [DataRow(CurlUrlPart.Query, CurlUrlCode.NoQuery)]
    [DataRow(CurlUrlPart.Fragment, CurlUrlCode.NoFragment)]
    [DataRow(CurlUrlPart.ZoneId, CurlUrlCode.NoZoneId)]
    public void Get_PartTheUrlLacks_ReturnsUpstreamsNoCode(CurlUrlPart part, CurlUrlCode expected)
    {
        using ManagedCurlUrlHandle url = Parsed("http://example.com/");

        Assert.AreEqual(expected, url.Get(part, CurlUrlFlags.None, out string? value));
        Assert.IsNull(value);
    }

    [TestMethod]
    [DataRow(CurlUrlPart.Url, CurlUrlCode.NoHost)]
    [DataRow(CurlUrlPart.Scheme, CurlUrlCode.NoScheme)]
    [DataRow(CurlUrlPart.Host, CurlUrlCode.NoHost)]
    [DataRow(CurlUrlPart.Port, CurlUrlCode.NoPort)]
    public void Get_NewHandle_ReturnsUpstreamsNoCode(CurlUrlPart part, CurlUrlCode expected)
    {
        using ManagedCurlUrlHandle url = new();

        Assert.AreEqual(expected, url.Get(part, CurlUrlFlags.DefaultPort, out string? value));
        Assert.IsNull(value);
    }

    [TestMethod]
    public void Get_PathOfANewHandle_ReturnsSlash()
    {
        using ManagedCurlUrlHandle url = new();

        Assert.AreEqual(CurlUrlCode.Ok, url.Get(CurlUrlPart.Path, CurlUrlFlags.None, out string? value));
        Assert.AreEqual("/", value);
    }

    [TestMethod]
    [DataRow(11)]
    [DataRow(-1)]
    public void Get_PartUpstreamDoesNotDefine_ReturnsUnknownPart(int part)
    {
        using ManagedCurlUrlHandle url = Parsed(FullUrl);

        Assert.AreEqual(CurlUrlCode.UnknownPart, url.Get((CurlUrlPart)part, CurlUrlFlags.None, out string? value));
        Assert.IsNull(value);
    }

    [TestMethod]
    public void Get_LoginOptionsOfAnImapUrl_ReturnsThemAndKeepsThemInTheUrl()
    {
        using ManagedCurlUrlHandle url = Parsed("imap://u;AUTH=PLAIN@mail.example/INBOX");

        Assert.AreEqual("AUTH=PLAIN", GetOk(url, CurlUrlPart.Options));
        Assert.AreEqual("imap://u;AUTH=PLAIN@mail.example/INBOX", GetOk(url, CurlUrlPart.Url));
    }

    [TestMethod]
    public void Get_EmptyUserAndAPassword_KeepsTheColonAndAt()
    {
        using ManagedCurlUrlHandle url = Parsed("http://:pw@h/");

        Assert.AreEqual(string.Empty, GetOk(url, CurlUrlPart.User));
        Assert.AreEqual("http://:pw@h/", GetOk(url, CurlUrlPart.Url));
    }

    [TestMethod]
    public void Get_IPv6HostWithZoneId_ReturnsHostAndZoneApartAndBothInTheUrl()
    {
        using ManagedCurlUrlHandle url = Parsed("http://[fe80::1%25eth0]:8080/");

        Assert.AreEqual("[fe80::1]", GetOk(url, CurlUrlPart.Host));
        Assert.AreEqual("eth0", GetOk(url, CurlUrlPart.ZoneId));
        Assert.AreEqual("http://[fe80::1%25eth0]:8080/", GetOk(url, CurlUrlPart.Url, CurlUrlFlags.UrlEncode));
    }

    [TestMethod]
    public void Get_IPv6HostWithoutZoneId_KeepsTheBracketsInTheUrl()
    {
        using ManagedCurlUrlHandle url = Parsed("http://[::1]");

        Assert.AreEqual("http://[::1]/", GetOk(url, CurlUrlPart.Url, CurlUrlFlags.UrlEncode));
    }

    [TestMethod]
    [DataRow("http://h:80/", "80")]
    [DataRow("http://h:80", "80")]
    [DataRow("http://h:080/", "80")]
    [DataRow("http://u@[::1]:80/", "80")]
    [DataRow("http://h:8080/", "8080")]
    public void Get_PortWritten_ReturnsItWithoutLeadingZeros(string text, string expected)
    {
        using ManagedCurlUrlHandle url = Parsed(text);

        Assert.AreEqual(expected, GetOk(url, CurlUrlPart.Port));
    }

    [TestMethod]
    [DataRow("http://h:/")]
    [DataRow("http://[::1]/")]
    [DataRow("http://h/?x=:80")]
    public void Get_NoPortWritten_ReturnsNoPort(string text)
    {
        using ManagedCurlUrlHandle url = Parsed(text);

        Assert.AreEqual(CurlUrlCode.NoPort, url.Get(CurlUrlPart.Port, CurlUrlFlags.None, out _));
    }

    [TestMethod]
    public void Get_DefaultPortFlagWithNoPortWritten_ReturnsTheSchemesDefault()
    {
        using ManagedCurlUrlHandle url = Parsed("http://h/");

        Assert.AreEqual("80", GetOk(url, CurlUrlPart.Port, CurlUrlFlags.DefaultPort));
        Assert.AreEqual("http://h:80/", GetOk(url, CurlUrlPart.Url, CurlUrlFlags.DefaultPort));
        Assert.AreEqual("http://h/", GetOk(url, CurlUrlPart.Url));
    }

    [TestMethod]
    public void Get_DefaultPortFlagForASchemeWithoutOne_ReturnsNoPort()
    {
        using ManagedCurlUrlHandle url = Parsed("foo://h/", CurlUrlFlags.NonSupportScheme);

        Assert.AreEqual(CurlUrlCode.NoPort, url.Get(CurlUrlPart.Port, CurlUrlFlags.DefaultPort, out _));
        Assert.AreEqual("foo://h/", GetOk(url, CurlUrlPart.Url, CurlUrlFlags.DefaultPort));
    }

    [TestMethod]
    public void Get_NoDefaultPortFlagWithTheDefaultWritten_HidesIt()
    {
        using ManagedCurlUrlHandle url = Parsed("http://h:80/");

        Assert.AreEqual(CurlUrlCode.NoPort, url.Get(CurlUrlPart.Port, CurlUrlFlags.NoDefaultPort, out _));
        Assert.AreEqual("http://h/", GetOk(url, CurlUrlPart.Url, CurlUrlFlags.NoDefaultPort));
        Assert.AreEqual("http://h:80/", GetOk(url, CurlUrlPart.Url));
    }

    [TestMethod]
    public void Get_NoDefaultPortFlagWithAnotherPortWritten_KeepsIt()
    {
        using ManagedCurlUrlHandle url = Parsed("http://h:81/");

        Assert.AreEqual("81", GetOk(url, CurlUrlPart.Port, CurlUrlFlags.NoDefaultPort));
    }

    [TestMethod]
    public void Get_PortWithNoSchemeSet_IgnoresBothPortFlags()
    {
        using ManagedCurlUrlHandle unset = new(new UrlHandleParts { Host = "h" });
        using ManagedCurlUrlHandle written = new(new UrlHandleParts { Host = "h", Port = 80 });

        Assert.AreEqual(CurlUrlCode.NoPort, unset.Get(CurlUrlPart.Port, CurlUrlFlags.DefaultPort, out _));
        Assert.AreEqual("80", GetOk(written, CurlUrlPart.Port, CurlUrlFlags.NoDefaultPort));
    }

    [TestMethod]
    public void Get_UrlWithAHostAndNoScheme_ReturnsNoSchemeUnlessDefaultSchemeAsks()
    {
        using ManagedCurlUrlHandle url = new(new UrlHandleParts { Host = "h", Path = "/x" });

        Assert.AreEqual(CurlUrlCode.NoScheme, url.Get(CurlUrlPart.Url, CurlUrlFlags.None, out _));
        Assert.AreEqual("https://h/x", GetOk(url, CurlUrlPart.Url, CurlUrlFlags.DefaultScheme));
        Assert.AreEqual("https://h:443/", GetOk(new ManagedCurlUrlHandle(new UrlHandleParts { Host = "h" }), CurlUrlPart.Url, CurlUrlFlags.DefaultScheme | CurlUrlFlags.DefaultPort));
    }

    [TestMethod]
    public void Set_NoSchemeWithDefaultScheme_UsesHttps()
    {
        using ManagedCurlUrlHandle url = new();

        Assert.AreEqual(CurlUrlCode.Ok, url.Set(CurlUrlPart.Url, "ftp.example/x", CurlUrlFlags.DefaultScheme | CurlUrlFlags.GuessScheme));

        Assert.AreEqual("https", GetOk(url, CurlUrlPart.Scheme, CurlUrlFlags.NoGuessScheme));
        Assert.AreEqual("https://ftp.example/x", GetOk(url, CurlUrlPart.Url, CurlUrlFlags.NoGuessScheme));
    }

    [TestMethod]
    public void Get_GuessedScheme_ReturnedUnlessNoGuessSchemeAsks()
    {
        using ManagedCurlUrlHandle url = Parsed("ftp.example:21/x", CurlUrlFlags.GuessScheme);

        Assert.AreEqual("ftp", GetOk(url, CurlUrlPart.Scheme));
        Assert.AreEqual("ftp://ftp.example:21/x", GetOk(url, CurlUrlPart.Url));
        Assert.AreEqual("21", GetOk(url, CurlUrlPart.Port));
        Assert.AreEqual(CurlUrlCode.NoScheme, url.Get(CurlUrlPart.Scheme, CurlUrlFlags.NoGuessScheme, out _));
        Assert.AreEqual("ftp.example:21/x", GetOk(url, CurlUrlPart.Url, CurlUrlFlags.NoGuessScheme));
    }

    [TestMethod]
    public void Get_WrittenSchemeWithNoGuessScheme_ReturnsIt()
    {
        using ManagedCurlUrlHandle url = Parsed("HTTP://h/");

        Assert.AreEqual("http", GetOk(url, CurlUrlPart.Scheme, CurlUrlFlags.NoGuessScheme));
        Assert.AreEqual("http://h/", GetOk(url, CurlUrlPart.Url, CurlUrlFlags.NoGuessScheme));
    }

    [TestMethod]
    public void Set_NoSchemeWithoutGuessOrDefault_ReturnsBadSchemeAndKeepsTheHandle()
    {
        // A handle holding a whole URL would resolve the text against it (BL-2437), so this one holds a host only.
        using ManagedCurlUrlHandle url = new(new UrlHandleParts { Host = "kept" });

        Assert.AreEqual(CurlUrlCode.BadScheme, url.Set(CurlUrlPart.Url, "example.com/x", CurlUrlFlags.None));

        Assert.AreEqual("kept", GetOk(url, CurlUrlPart.Host));
    }

    [TestMethod]
    [DataRow("1h://example.net", CurlUrlFlags.None)]
    [DataRow("..://example.net", CurlUrlFlags.None)]
    [DataRow("-ht://example.net", CurlUrlFlags.None)]
    [DataRow("+ftp://example.net", CurlUrlFlags.None)]
    [DataRow("htt ps://user:password@example.net/get?this=and-what", CurlUrlFlags.NonSupportScheme | CurlUrlFlags.AllowSpace)]
    [DataRow("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA://hostname/path", CurlUrlFlags.None)]
    [DataRow("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA://hostname/path", CurlUrlFlags.NonSupportScheme)]
    public void Set_UrlWhoseSchemeIsMalformedOrTooLong_ReturnsBadScheme(string text, CurlUrlFlags flags)
    {
        // lib1560's get_parts rows 39 to 42 and 92, get_url row 146 and its huge-scheme row (BL-2732).
        using ManagedCurlUrlHandle url = new();

        Assert.AreEqual(CurlUrlCode.BadScheme, url.Set(CurlUrlPart.Url, text, flags));
    }

    [TestMethod]
    public void Set_HugeSchemeUnderNonSupportScheme_ReturnsBadScheme()
    {
        using ManagedCurlUrlHandle url = new();

        Assert.AreEqual(CurlUrlCode.BadScheme, url.Set(CurlUrlPart.Url, new string('a', 119_998) + "://h/", CurlUrlFlags.NonSupportScheme));
    }

    [TestMethod]
    [DataRow(1, CurlUrlPart.User, "")]
    [DataRow(2, CurlUrlPart.Password, "")]
    [DataRow(3, CurlUrlPart.Host, "")]
    [DataRow(4, CurlUrlPart.Path, "/")]
    [DataRow(5, CurlUrlPart.Query, "")]
    [DataRow(6, CurlUrlPart.Fragment, "")]
    public void Set_OneLetterSchemeWithAHugePartUnderNonSupportScheme_ParsesAndReadsThePartBackWhole(int index, CurlUrlPart part, string prefix)
    {
        // lib1560's huge group (BL-2791): c://c:c@c/c?c#c with one part 119,998 bytes long.
        string big = new('a', 119_998);
        string[] pieces = [.. Enumerable.Range(0, 7).Select(piece => piece == index ? big : "c")];
        string text = $"{pieces[0]}://{pieces[1]}:{pieces[2]}@{pieces[3]}/{pieces[4]}?{pieces[5]}#{pieces[6]}";
        using ManagedCurlUrlHandle url = new();

        Assert.AreEqual(CurlUrlCode.Ok, url.Set(CurlUrlPart.Url, text, CurlUrlFlags.NonSupportScheme));
        Assert.AreEqual(CurlUrlCode.Ok, url.Get(part, CurlUrlFlags.None, out string? value));
        Assert.AreEqual(prefix + big, value);
    }

    [TestMethod]
    [DataRow("example.net:81/x", CurlUrlFlags.None, CurlUrlCode.UnsupportedScheme)]
    [DataRow("Example.net:81/x", CurlUrlFlags.NonSupportScheme, CurlUrlCode.BadSlashes)]
    [DataRow("http:81/x", CurlUrlFlags.None, CurlUrlCode.BadSlashes)]
    public void Set_HostAndPortWithoutGuessFlags_ReadsTheHostAsAScheme(string text, CurlUrlFlags flags, CurlUrlCode expected)
    {
        using ManagedCurlUrlHandle url = new();

        Assert.AreEqual(expected, url.Set(CurlUrlPart.Url, text, flags));
    }

    [TestMethod]
    [DataRow("data:text/html;charset=utf-8;base64,PCFET0NUWVBFIEhUTUw+PG1ldGEgY")]
    [DataRow("d:anything-really")]
    [DataRow("about:config")]
    [DataRow("mailto:infobot@example.com?body=send%20current-issue")]
    public void Set_SchemeOnlyUrlWithAnUnsupportedScheme_ReturnsUnsupportedScheme(string text)
    {
        // lib1560's get_url rows 106 to 108 and 110 (BL-2733).
        using ManagedCurlUrlHandle url = new();

        Assert.AreEqual(CurlUrlCode.UnsupportedScheme, url.Set(CurlUrlPart.Url, text, CurlUrlFlags.None));
    }

    [TestMethod]
    public void Set_RelativeReferenceOnAUrlWithAnUnsupportedScheme_ReturnsUnsupportedScheme()
    {
        using ManagedCurlUrlHandle url = new();
        Assert.AreEqual(CurlUrlCode.Ok, url.Set(CurlUrlPart.Url, "foo://example.com/a", CurlUrlFlags.NonSupportScheme));

        Assert.AreEqual(CurlUrlCode.UnsupportedScheme, url.Set(CurlUrlPart.Url, "b", CurlUrlFlags.None));
    }

    [TestMethod]
    public void Set_PortLikeTextWithoutASchemeUnderGuessScheme_IsStillParsedAsAHost()
    {
        using ManagedCurlUrlHandle url = new();

        Assert.AreEqual(CurlUrlCode.Ok, url.Set(CurlUrlPart.Url, "example.net:81/x", CurlUrlFlags.GuessScheme));
        Assert.AreEqual("81", GetOk(url, CurlUrlPart.Port));
    }

    [TestMethod]
    [DataRow("http://h /", CurlUrlCode.MalformedInput)]
    [DataRow("http:////h/", CurlUrlCode.BadSlashes)]
    [DataRow("http:///", CurlUrlCode.NoHost)]
    [DataRow("http://h:99999/", CurlUrlCode.BadPortNumber)]
    [DataRow("http://[::g]/", CurlUrlCode.BadIPv6)]
    [DataRow("http://h<x/", CurlUrlCode.BadHostname)]
    [DataRow("file://remote/x", CurlUrlCode.BadFileUrl)]
    public void Set_UrlCurlRejects_ReturnsTheRejectionsCode(string text, CurlUrlCode expected)
    {
        using ManagedCurlUrlHandle url = new();

        Assert.AreEqual(expected, url.Set(CurlUrlPart.Url, text, CurlUrlFlags.None));
    }

    // lib1560's get_parts row 132 and get_url rows 189 to 193 (BL-2737): curl 8.22.0's parse_file.
    [TestMethod]
    [DataRow("file:/")]
    [DataRow("file:./")]
    [DataRow("file:foo")]
    [DataRow("file:foo/bar")]
    [DataRow("file:?q")]
    [DataRow("file:#f")]
    public void Set_FileUrlTooShortOrWithARelativePath_ReturnsBadFileUrl(string text)
    {
        using ManagedCurlUrlHandle url = new();

        Assert.AreEqual(CurlUrlCode.BadFileUrl, url.Set(CurlUrlPart.Url, text, CurlUrlFlags.None));
    }

    // lib1560's Windows get_parts rows 105 and 106 (BL-2737): libcurl keeps a drive path's backslashes.
    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    [DataRow("file://C:\\programs\\foo")]
    [DataRow("file:///C:\\programs\\foo")]
    public void Set_FileUrlWithADriveAndBackslashesOnWindows_KeepsTheBackslashesInThePath(string text)
    {
        using ManagedCurlUrlHandle url = Parsed(text, CurlUrlFlags.DefaultScheme);

        Assert.AreEqual("C:\\programs\\foo", GetOk(url, CurlUrlPart.Path));
    }

    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
    [DataRow("file://C:\\programs\\foo")]
    [DataRow("file:///C:\\programs\\foo")]
    public void Set_FileUrlWithADriveOffWindows_ReturnsBadFileUrl(string text)
    {
        using ManagedCurlUrlHandle url = new();

        Assert.AreEqual(CurlUrlCode.BadFileUrl, url.Set(CurlUrlPart.Url, text, CurlUrlFlags.DefaultScheme));
    }

    // lib1560's get_parts rows 0 to 2 (BL-2731): a backslash where the port's digits end.
    [TestMethod]
    [DataRow("http:\\\\hostname", CurlUrlFlags.GuessScheme)]
    [DataRow("http:\\\\hostname:1234", CurlUrlFlags.GuessScheme)]
    [DataRow("http://hostname:1111\\path", CurlUrlFlags.None)]
    public void Set_UrlWithABackslashWhereThePortEnds_ReturnsBackslash(string text, CurlUrlFlags flags)
    {
        using ManagedCurlUrlHandle url = new();

        Assert.AreEqual(CurlUrlCode.Backslash, url.Set(CurlUrlPart.Url, text, flags));
    }

    [TestMethod]
    public void Set_UserWithDisallowUser_ReturnsUserNotAllowed()
    {
        using ManagedCurlUrlHandle url = new();

        Assert.AreEqual(CurlUrlCode.UserNotAllowed, url.Set(CurlUrlPart.Url, "http://u@h/", CurlUrlFlags.DisallowUser));
        Assert.AreEqual(CurlUrlCode.Ok, url.Set(CurlUrlPart.Url, "http://h/", CurlUrlFlags.DisallowUser));
    }

    [TestMethod]
    public void Get_PathParsedWithAndWithoutPathAsIs_KeepsOrRemovesDotSegments()
    {
        using ManagedCurlUrlHandle cleaned = Parsed("http://h/a/../b");
        using ManagedCurlUrlHandle asIs = Parsed("http://h/a/../b", CurlUrlFlags.PathAsIs);

        Assert.AreEqual("/b", GetOk(cleaned, CurlUrlPart.Path));
        Assert.AreEqual("/a/../b", GetOk(asIs, CurlUrlPart.Path));
    }

    [TestMethod]
    public void Get_FileUrl_HasNoHostAndRebuildsFromThePath()
    {
        using ManagedCurlUrlHandle url = Parsed("file:///dir/x?q#f");

        Assert.AreEqual(CurlUrlCode.NoHost, url.Get(CurlUrlPart.Host, CurlUrlFlags.None, out _));
        Assert.AreEqual(CurlUrlCode.NoPort, url.Get(CurlUrlPart.Port, CurlUrlFlags.None, out _));
        Assert.AreEqual("file:///dir/x?q#f", GetOk(url, CurlUrlPart.Url));
    }

    [TestMethod]
    public void Get_BareQueryAndFragment_GivenOnlyUnderGetEmpty()
    {
        using ManagedCurlUrlHandle url = Parsed("http://h/?#");

        Assert.AreEqual(CurlUrlCode.NoQuery, url.Get(CurlUrlPart.Query, CurlUrlFlags.None, out _));
        Assert.AreEqual(CurlUrlCode.NoFragment, url.Get(CurlUrlPart.Fragment, CurlUrlFlags.None, out _));
        Assert.AreEqual(string.Empty, GetOk(url, CurlUrlPart.Query, CurlUrlFlags.GetEmpty));
        Assert.AreEqual(string.Empty, GetOk(url, CurlUrlPart.Fragment, CurlUrlFlags.GetEmpty));
        Assert.AreEqual("http://h/", GetOk(url, CurlUrlPart.Url));
        Assert.AreEqual("http://h/?#", GetOk(url, CurlUrlPart.Url, CurlUrlFlags.GetEmpty));
    }

    [TestMethod]
    [DataRow(CurlUrlPart.Path, "/a%20b%zz", "/a b%zz")]
    [DataRow(CurlUrlPart.Query, "a+b%3D%C3%A9", "a b=\u00e9")]
    [DataRow(CurlUrlPart.Fragment, "a+b", "a+b")]
    [DataRow(CurlUrlPart.User, "us%40er", "us@er")]
    public void Get_UrlDecode_DecodesThePart(CurlUrlPart part, string held, string expected)
    {
        using ManagedCurlUrlHandle url = new(new UrlHandleParts
        {
            Scheme = "http",
            Host = "h",
            Path = held,
            Query = held,
            Fragment = held,
            User = held,
        });

        Assert.AreEqual(expected, GetOk(url, part, CurlUrlFlags.UrlDecode | CurlUrlFlags.UrlEncode));
    }

    [TestMethod]
    [DataRow("/a%00b")]
    [DataRow("/a%1Fb")]
    [DataRow("/a\tb")]
    public void Get_UrlDecodeToAControlByte_ReturnsUrlDecode(string path)
    {
        using ManagedCurlUrlHandle url = new(new UrlHandleParts { Path = path });

        Assert.AreEqual(CurlUrlCode.UrlDecode, url.Get(CurlUrlPart.Path, CurlUrlFlags.UrlDecode, out string? value));
        Assert.IsNull(value);
    }

    [TestMethod]
    [DataRow(CurlUrlPart.Path, "/a b\u00e9\u0001", "/a%20b%C3%A9%01")]
    [DataRow(CurlUrlPart.Path, "/x?y z", "/x?y+z")]
    [DataRow(CurlUrlPart.Query, "a b", "a+b")]
    [DataRow(CurlUrlPart.Fragment, "a\u007fb", "a%7Fb")]
    public void Get_UrlEncode_EncodesThePart(CurlUrlPart part, string held, string expected)
    {
        using ManagedCurlUrlHandle url = new(new UrlHandleParts { Path = held, Query = held, Fragment = held });

        Assert.AreEqual(expected, GetOk(url, part, CurlUrlFlags.UrlEncode));
    }

    [TestMethod]
    public void Get_UrlEncodeOfTheWholeUrl_EscapesTheHost()
    {
        using ManagedCurlUrlHandle url = new(new UrlHandleParts { Scheme = "http", Host = "a b~\u00e9", Path = "/" });

        Assert.AreEqual("http://a%20b~%C3%A9/", GetOk(url, CurlUrlPart.Url, CurlUrlFlags.UrlEncode));
    }

    [TestMethod]
    public void Get_PunycodeOfAnInternationalHost_ReturnsPunycode()
    {
        using ManagedCurlUrlHandle url = new(new UrlHandleParts { Scheme = "http", Host = "r\u00e4ksm\u00f6rg\u00e5s.se" });

        Assert.AreEqual("xn--rksmrgs-5wao1o.se", GetOk(url, CurlUrlPart.Host, CurlUrlFlags.Punycode));
        Assert.AreEqual("http://xn--rksmrgs-5wao1o.se/", GetOk(url, CurlUrlPart.Url, CurlUrlFlags.Punycode));
        Assert.AreEqual("r\u00e4ksm\u00f6rg\u00e5s.se", GetOk(url, CurlUrlPart.Host));
    }

    [TestMethod]
    public void Get_PunycodeOfAnAsciiHost_LeavesItEvenWithPunycodeToIdn()
    {
        using ManagedCurlUrlHandle url = new(new UrlHandleParts { Host = "xn--rksmrgs-5wao1o.se" });

        Assert.AreEqual("xn--rksmrgs-5wao1o.se", GetOk(url, CurlUrlPart.Host, CurlUrlFlags.Punycode | CurlUrlFlags.PunycodeToIdn));
    }

    [TestMethod]
    public void Get_PunycodeToIdnOfAPunycodeHost_ReturnsTheInternationalName()
    {
        using ManagedCurlUrlHandle url = new(new UrlHandleParts { Scheme = "http", Host = "xn--rksmrgs-5wao1o.se" });
        using ManagedCurlUrlHandle plain = new(new UrlHandleParts { Host = "example.com" });

        Assert.AreEqual("r\u00e4ksm\u00f6rg\u00e5s.se", GetOk(url, CurlUrlPart.Host, CurlUrlFlags.PunycodeToIdn));
        Assert.AreEqual("http://r\u00e4ksm\u00f6rg\u00e5s.se/", GetOk(url, CurlUrlPart.Url, CurlUrlFlags.PunycodeToIdn));
        Assert.AreEqual("example.com", GetOk(plain, CurlUrlPart.Host, CurlUrlFlags.PunycodeToIdn));
    }

    [TestMethod]
    public void Get_PunycodeHostThatDoesNotConvert_ReturnsBadHostname()
    {
        using ManagedCurlUrlHandle idn = new(new UrlHandleParts { Scheme = "http", Host = "xn--" });
        using ManagedCurlUrlHandle unicode = new(new UrlHandleParts { Host = "\u00e9..x" });

        Assert.AreEqual(CurlUrlCode.BadHostname, idn.Get(CurlUrlPart.Url, CurlUrlFlags.PunycodeToIdn, out string? value));
        Assert.IsNull(value);
        Assert.AreEqual(CurlUrlCode.BadHostname, unicode.Get(CurlUrlPart.Host, CurlUrlFlags.Punycode, out _));
    }


    [TestMethod]
    public void Dispose_Twice_DoesNotThrow()
    {
        ManagedCurlUrlHandle url = Parsed(FullUrl);

        url.Dispose();
        url.Dispose();

        Assert.AreEqual(FullUrl, GetOk(url, CurlUrlPart.Url));
    }

    // A lone surrogate cannot sit in an attribute's UTF-8 string, so these rows are not DataRows.
    [TestMethod]
    public void Get_UrlWithAHostByteThatIsNotUtf8_KeepsTheByte()
    {
        using ManagedCurlUrlHandle url = Parsed("https://_%c0_");

        Assert.AreEqual("https://_\uDCC0_/", GetOk(url, CurlUrlPart.Url));
    }

    [TestMethod]
    public void Get_UrlEncodedWithAHostByteThatIsNotUtf8_EscapesTheByte()
    {
        using ManagedCurlUrlHandle url = Parsed("https://_%c0_");

        Assert.AreEqual("https://_%C0_/", GetOk(url, CurlUrlPart.Url, CurlUrlFlags.UrlEncode));
    }

    [TestMethod]
    public void Get_UrlEncodedWithARawByteThatIsNotUtf8_EscapesTheByte()
    {
        using ManagedCurlUrlHandle url = Parsed("https://\uDCFF.127.0.0.1");

        Assert.AreEqual("https://%FF.127.0.0.1/", GetOk(url, CurlUrlPart.Url, CurlUrlFlags.UrlEncode));
    }

    [TestMethod]
    public void Get_DecodedPathWithAByteThatIsNotUtf8_IsDc00PlusTheByte()
    {
        using ManagedCurlUrlHandle url = Parsed("https://h/_%c0_");

        Assert.AreEqual("/_\uDCC0_", GetOk(url, CurlUrlPart.Path, CurlUrlFlags.UrlDecode));
    }

    private static ManagedCurlUrlHandle Parsed(string text, CurlUrlFlags flags = CurlUrlFlags.None)
    {
        ManagedCurlUrlHandle url = new();
        Assert.AreEqual(CurlUrlCode.Ok, url.Set(CurlUrlPart.Url, text, flags));

        return url;
    }

    private static string? GetOk(ManagedCurlUrlHandle url, CurlUrlPart part, CurlUrlFlags flags = CurlUrlFlags.None)
    {
        Assert.AreEqual(CurlUrlCode.Ok, url.Get(part, flags, out string? value));

        return value;
    }
}
