namespace Curl.Protocol.Abstractions;

/// <summary>
/// Pins why <see cref="CurlUrl" /> rejects each URL to the message curl 8.21.0 (the mingw
/// build, <c>/mingw64/bin/curl -gsS</c>) printed after <c>URL rejected: </c> for it,
/// measured on 2026-09-27 (BL-324).
/// </summary>
[TestClass]
public sealed class CurlUrlRejectionTests
{
    [TestMethod]
    [DataRow("http://h/a b", CurlUrlRejection.MalformedInput)]
    [DataRow("http://h/\x7f", CurlUrlRejection.MalformedInput)]
    [DataRow("http:////h/", CurlUrlRejection.BadSlashes)]
    [DataRow("http:////", CurlUrlRejection.BadSlashes)]
    [DataRow("http:/", CurlUrlRejection.NoHost)]
    [DataRow("http://", CurlUrlRejection.NoHost)]
    [DataRow("http:///", CurlUrlRejection.NoHost)]
    [DataRow("http://?x", CurlUrlRejection.NoHost)]
    [DataRow("http://u@/", CurlUrlRejection.NoHost)]
    [DataRow("http://u:p@/", CurlUrlRejection.NoHost)]
    [DataRow("http://:80/", CurlUrlRejection.NoHost)]
    [DataRow(":80/x", CurlUrlRejection.NoHost)]
    [DataRow("http://h:99999/", CurlUrlRejection.BadPortNumber)]
    [DataRow("http://h:1x/", CurlUrlRejection.BadPortNumber)]
    [DataRow("http://h:-1/", CurlUrlRejection.BadPortNumber)]
    [DataRow("http://:x/", CurlUrlRejection.BadPortNumber)]
    [DataRow("http://[::1]x/", CurlUrlRejection.BadPortNumber)]
    [DataRow("http://[::1]]/", CurlUrlRejection.BadPortNumber)]
    [DataRow("http://[::1]:x/", CurlUrlRejection.BadPortNumber)]
    [DataRow("http:x", CurlUrlRejection.BadPortNumber)]
    [DataRow("x:///", CurlUrlRejection.BadPortNumber)]
    [DataRow("http://[::1/", CurlUrlRejection.BadIPv6)]
    [DataRow("http://[::g]/", CurlUrlRejection.BadIPv6)]
    [DataRow("http://[::1%]/", CurlUrlRejection.BadIPv6)]
    [DataRow("http://[::1%25]/", CurlUrlRejection.BadIPv6)]
    [DataRow("http://[:::1]/", CurlUrlRejection.BadIPv6)]
    [DataRow("http://[1.2.3]/", CurlUrlRejection.BadIPv6)]
    [DataRow("http://[]/", CurlUrlRejection.BadIPv6)]
    [DataRow("http://[::1%1234567890123456]/", CurlUrlRejection.BadHostname)]
    [DataRow("http://a!b/", CurlUrlRejection.BadHostname)]
    [DataRow("http://a%01b/", CurlUrlRejection.BadHostname)]
    [DataRow("http://a%zz/", CurlUrlRejection.BadHostname)]
    [DataRow("http://./", CurlUrlRejection.BadHostname)]
    [DataRow("http://a../", CurlUrlRejection.BadHostname)]
    [DataRow("a!b", CurlUrlRejection.BadHostname)]
    [DataRow("file://example.com/x", CurlUrlRejection.BadFileUrl)]
    [DataRow("file://[::1]/x", CurlUrlRejection.BadFileUrl)]
    [DataRow("file://localhost", CurlUrlRejection.BadFileUrl)]
    public void TryParse_WithAUrlCurlRejects_SaysWhy(string text, CurlUrlRejection expected)
    {
        bool parsed = CurlUrl.TryParse(text, pathAsIs: false, driveLetters: true, out CurlUrl? url, out CurlUrlRejection rejection);

        Assert.IsFalse(parsed);
        Assert.IsNull(url);
        Assert.AreEqual(expected, rejection);
    }

    [TestMethod]
    public void TryParse_WithTextLongerThanCurlAccepts_SaysTheInputIsMalformed()
    {
        string text = "http://h/" + new string('a', 8_000_000);

        CurlUrl.TryParse(text, pathAsIs: false, driveLetters: true, out _, out CurlUrlRejection rejection);

        Assert.AreEqual(CurlUrlRejection.MalformedInput, rejection);
    }

    // curl outside Windows refuses a drive letter in a file URL with CURLUE_BAD_FILE_URL
    // (lib/urlapi.c at 8.21.0); the Windows build accepts it.
    [TestMethod]
    public void TryParse_WithADriveLetterWhereCurlRefusesOne_SaysTheFileUrlIsBad()
    {
        CurlUrl.TryParse("file:///C:/x", pathAsIs: false, driveLetters: false, out _, out CurlUrlRejection rejection);

        Assert.AreEqual(CurlUrlRejection.BadFileUrl, rejection);
    }

    [TestMethod]
    [DataRow("http://h/")]
    [DataRow("http://[::1%zz]/")]
    [DataRow("http://1.2.3.4/")]
    [DataRow("file:///C:/x")]
    public void TryParse_WithAUrlCurlAccepts_SaysNone(string text)
    {
        bool parsed = CurlUrl.TryParse(text, pathAsIs: false, driveLetters: true, out CurlUrl? url, out CurlUrlRejection rejection);

        Assert.IsTrue(parsed);
        Assert.IsNotNull(url);
        Assert.AreEqual(CurlUrlRejection.None, rejection);
    }

    // curl 8.21.0 on Windows, measured 2026-10-07 (BL-1526, ADR-0418): curl -sSv
    // "file:///C:/dir/f.txt%00x" passes URL parsing and fails in file_connect with
    // "URL using bad/illegal format or missing URL", as the drive-less form does; only a
    // space in the path, as in a profile directory, makes the parser say "URL rejected".
    [TestMethod]
    public void TryParse_WithADriveLetterPathEscapingANul_AcceptsIt()
    {
        bool parsed = CurlUrl.TryParse("file:///C:/dir/f.txt%00x", pathAsIs: false, driveLetters: true, out _, out CurlUrlRejection rejection);

        Assert.IsTrue(parsed);
        Assert.AreEqual(CurlUrlRejection.None, rejection);
    }

    [TestMethod]
    public void TryParse_WithADriveLetterPathHoldingASpace_SaysTheInputIsMalformed()
    {
        bool parsed = CurlUrl.TryParse("file:///C:/Users/Stewart Rogers/f.txt%00x", pathAsIs: false, driveLetters: true, out _, out CurlUrlRejection rejection);

        Assert.IsFalse(parsed);
        Assert.AreEqual(CurlUrlRejection.MalformedInput, rejection);
    }

    [TestMethod]
    public void TryParse_OnThePublicOverloadWithAReason_SaysWhy()
    {
        bool parsed = CurlUrl.TryParse("http:////h/", pathAsIs: false, out CurlUrl? url, out CurlUrlRejection rejection);

        Assert.IsFalse(parsed);
        Assert.IsNull(url);
        Assert.AreEqual(CurlUrlRejection.BadSlashes, rejection);
    }

    // curl 8.21.0 checks CURLU_DISALLOW_USER while parsing the login, before the host and port
    // (measured 2026-10-01, BL-910 Notes).
    [TestMethod]
    [DataRow("http://u@h/")]
    [DataRow("http://@h/")]
    [DataRow("http://u:p@h:abc/")]
    [DataRow("http://u@127.0.0.1:99999/")]
    [DataRow("http://u@exa%20mple.com/")]
    [DataRow("http://u@[::1]x/")]
    [DataRow("http://u@:80/")]
    [DataRow("http://u@/")]
    [DataRow("foo://u@h/")]
    public void TryParseDisallowingUser_WithUserInformation_SaysTheUserIsNotAllowed(string text)
    {
        bool parsed = CurlUrl.TryParseDisallowingUser(text, pathAsIs: false, out CurlUrl? url, out CurlUrlRejection rejection);

        Assert.IsFalse(parsed);
        Assert.IsNull(url);
        Assert.AreEqual(CurlUrlRejection.UserNotAllowed, rejection);
    }

    [TestMethod]
    [DataRow("http://h:99999/", CurlUrlRejection.BadPortNumber)]
    [DataRow("http:////u@h/", CurlUrlRejection.BadSlashes)]
    [DataRow("http://u@h/a b", CurlUrlRejection.MalformedInput)]
    public void TryParseDisallowingUser_WithARejectionBeforeOrWithoutALogin_SaysThatRejection(string text, CurlUrlRejection expected)
    {
        bool parsed = CurlUrl.TryParseDisallowingUser(text, pathAsIs: false, out _, out CurlUrlRejection rejection);

        Assert.IsFalse(parsed);
        Assert.AreEqual(expected, rejection);
    }

    [TestMethod]
    [DataRow("http://h/")]
    [DataRow("http://h/a@b")]
    [DataRow("file:///x")]
    public void TryParseDisallowingUser_WithoutUserInformation_Parses(string text)
    {
        bool parsed = CurlUrl.TryParseDisallowingUser(text, pathAsIs: false, out CurlUrl? url, out CurlUrlRejection rejection);

        Assert.IsTrue(parsed);
        Assert.IsNotNull(url);
        Assert.AreEqual(text, url.OriginalString);
        Assert.AreEqual(CurlUrlRejection.None, rejection);
    }

    [TestMethod]
    public void TryParse_OnThePublicOverloadWithAReasonAndNullText_ThrowsArgumentNullException()
    {
        string? text = null;

        var exception = Assert.ThrowsExactly<ArgumentNullException>(
            () => CurlUrl.TryParse(text!, pathAsIs: false, out _, out _));

        Assert.AreEqual("text", exception.ParamName);
    }

    [TestMethod]
    [DataRow(CurlUrlRejection.MalformedInput, "Malformed input to a URL function")]
    [DataRow(CurlUrlRejection.BadSlashes, "Unsupported number of slashes following scheme")]
    [DataRow(CurlUrlRejection.NoHost, "No host part in the URL")]
    [DataRow(CurlUrlRejection.BadPortNumber, "Port number was not a decimal number between 0 and 65535")]
    [DataRow(CurlUrlRejection.BadIPv6, "Bad IPv6 address")]
    [DataRow(CurlUrlRejection.BadHostname, "Bad hostname")]
    [DataRow(CurlUrlRejection.BadFileUrl, "Bad file:// URL")]
    [DataRow(CurlUrlRejection.UserNotAllowed, "Credentials was passed in the URL when prohibited")]
    public void ToCurlMessage_ForEachRejection_ReturnsCurlsText(CurlUrlRejection rejection, string expected)
    {
        Assert.AreEqual(expected, rejection.ToCurlMessage());
    }

    [TestMethod]
    [DataRow(CurlUrlRejection.None)]
    [DataRow((CurlUrlRejection)99)]
    public void ToCurlMessage_ForNoRejection_ThrowsArgumentOutOfRangeException(CurlUrlRejection rejection)
    {
        var exception = Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => rejection.ToCurlMessage());

        Assert.AreEqual("rejection", exception.ParamName);
    }
}
