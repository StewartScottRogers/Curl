using Curl.Testing;

namespace Curl.Protocol.Abstractions;

/// <summary>
/// Attacks <see cref="CurlUrl" /> through its public parse methods at its boundaries, with
/// malformed URLs and in its invalid partitions, and under concurrent calls, by the method in
/// <c>Documentation/Wiki/Adversarial-Testing.md</c> (BL-1505). Every expected answer was
/// measured on curl 8.21.0 (the Schannel mingw build) on 2026-10-07 with
/// <c>--proto =dict -w "%{exitcode}|%{url.scheme}|%{url.user}|%{url.password}|%{url.host}|%{url.port}|%{url.path}|%{url.zoneid}"</c>,
/// so curl parsed each URL and stopped before connecting: exit 1 means it accepted the URL,
/// exit 3 that it rejected it, with the message pinned as the rejection.
/// </summary>
[TestClass]
public sealed class CurlUrlAdversarialTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [DataRow("http://h:0/", 0)]
    [DataRow("http://h:65535/", 65535)]
    [DataRow("http://h:/", 80)]
    [DataRow("http://h:0080/", 80)]
    public void TryParse_WithAPortOnTheEdgeOfItsRange_ReadsThePortCurlReads(string text, int port)
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);

        CurlUrl url = AssertAccepted(diagnostics, text);

        diagnostics.Assert("port", port, url.Port);
        Assert.AreEqual(port, url.Port);
        Assert.AreEqual("h", url.Host);
    }

    [TestMethod]
    [DataRow("http://h:65536/")]
    [DataRow("http://h:99999999999999999999/")]
    [DataRow("http://h:-1/")]
    [DataRow("http://h:+80/")]
    [DataRow("http://user@:80@host/")]
    public void TryParse_WithAPortOutsideItsRangeOrNotDecimal_RejectsItAsABadPortNumber(string text)
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);

        AssertRejected(diagnostics, text, CurlUrlRejection.BadPortNumber);
    }

    [TestMethod]
    [DataRow("http://h: 80/")]
    [DataRow("http://h/a b")]
    public void TryParse_WithASpaceInThePortOrPath_RejectsItAsMalformedInput(string text)
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);

        AssertRejected(diagnostics, text, CurlUrlRejection.MalformedInput);
    }

    [TestMethod]
    public void TryParse_WithTwoAtSignsInTheAuthority_RejectsTheHostCurlRejects()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);

        AssertRejected(diagnostics, "http://a@b@c/", CurlUrlRejection.BadHostname);
    }

    [TestMethod]
    public void TryParse_WithNineGroupsInAnIPv6Literal_RejectsItAsABadIPv6Address()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);

        AssertRejected(diagnostics, "http://[1:2:3:4:5:6:7:8:9]/", CurlUrlRejection.BadIPv6);
    }

    [TestMethod]
    [DataRow("HTTP://H/", "H")]
    [DataRow("hTtP://h/", "h")]
    public void TryParse_WithASchemeInMixedCase_ReadsTheSchemeInLowerCase(string text, string host)
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);

        CurlUrl url = AssertAccepted(diagnostics, text);

        diagnostics.Assert("scheme", "http", url.Scheme);
        Assert.AreEqual("http", url.Scheme);
        Assert.AreEqual(host, url.Host);
        Assert.AreEqual(80, url.Port);
    }

    [TestMethod]
    public void TryParse_WithAnEmptyUserAndPassword_StillReadsTheHost()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);

        CurlUrl url = AssertAccepted(diagnostics, "http://:@h/");

        diagnostics.Assert("host", "h", url.Host);
        Assert.AreEqual("h", url.Host);
        Assert.AreEqual(80, url.Port);
    }

    [TestMethod]
    public void TryParse_WithPercentEncodedDelimitersInTheUserInformation_KeepsThemEncoded()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);

        CurlUrl url = AssertAccepted(diagnostics, "http://%40:%3A@h/");

        diagnostics.Assert("user", "%40", url.User);
        diagnostics.Assert("password", "%3A", url.Password);
        Assert.AreEqual("%40", url.User);
        Assert.AreEqual("%3A", url.Password);
        Assert.AreEqual("h", url.Host);
    }

    [TestMethod]
    [DataRow("http://[fe80::1%25eth0]:8080/", "[fe80::1]", 8080)]
    [DataRow("http://[::1%25eth0]/", "[::1]", 80)]
    public void TryParse_WithAZoneIdAndAPort_SplitsTheZoneFromTheHost(string text, string host, int port)
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);

        CurlUrl url = AssertAccepted(diagnostics, text);

        diagnostics.Assert("host", host, url.Host);
        diagnostics.Assert("zone id", "eth0", url.ZoneId);
        Assert.AreEqual(host, url.Host);
        Assert.AreEqual("eth0", url.ZoneId);
        Assert.AreEqual(port, url.Port);
    }

    [TestMethod]
    [DataRow("http://0x7f.1/", "127.0.0.1")]
    [DataRow("http://017700000001/", "127.0.0.1")]
    [DataRow("http://4294967295/", "255.255.255.255")]
    [DataRow("http://0xffffffff/", "255.255.255.255")]
    [DataRow("http://037777777777/", "255.255.255.255")]
    [DataRow("http://0/", "0.0.0.0")]
    [DataRow("http://00000000000000000000001/", "0.0.0.1")]
    public void TryParse_WithAShortOctalOrHexIPv4Form_NormalisesItAsCurlDoes(string text, string host)
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);

        CurlUrl url = AssertAccepted(diagnostics, text);

        diagnostics.Assert("host", host, url.Host);
        Assert.AreEqual(host, url.Host);
    }

    [TestMethod]
    [DataRow("http://4294967296/", "4294967296")]
    [DataRow("http://040000000000/", "040000000000")]
    [DataRow("http://0x100000000/", "0x100000000")]
    [DataRow("http://1.2.3.256/", "1.2.3.256")]
    [DataRow("http://1.16777216/", "1.16777216")]
    [DataRow("http://1.2.65536/", "1.2.65536")]
    [DataRow("http://08.1.1.1/", "08.1.1.1")]
    [DataRow("http://0xG/", "0xG")]
    [DataRow("http://0x/", "0x")]
    [DataRow("http://1.2.3.4.5/", "1.2.3.4.5")]
    public void TryParse_WithAnIPv4FormOutOfRange_KeepsItAsAHostName(string text, string host)
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);

        CurlUrl url = AssertAccepted(diagnostics, text);

        diagnostics.Assert("host", host, url.Host);
        Assert.AreEqual(host, url.Host);
    }

    [TestMethod]
    [DataRow("http://h/a%00b", "/a%00b")]
    [DataRow("http://h/a%zzb", "/a%zzb")]
    [DataRow("http://h/%", "/%")]
    [DataRow("http://h/a/../../../x", "/x")]
    [DataRow("http://h/./././", "/")]
    public void TryParse_WithABrokenEscapeOrTooManyDotSegmentsInThePath_KeepsThePathCurlKeeps(string text, string path)
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);

        CurlUrl url = AssertAccepted(diagnostics, text);

        diagnostics.Assert("path", path, url.AbsolutePath);
        Assert.AreEqual(path, url.AbsolutePath);
    }

    [TestMethod]
    [DataRow("http://h:65536/")]
    [DataRow("http://a@b@c/")]
    [DataRow("http://[::1")]
    public void TryParse_AfterARejection_StillParsesTheNextUrl(string rejected)
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("rejected first", rejected);

        bool firstParsed = CurlUrl.TryParse(rejected, pathAsIs: false, out _, out _);
        CurlUrl url = AssertAccepted(diagnostics, "http://h:81/p");

        diagnostics.Act("first parsed", firstParsed);
        Assert.IsFalse(firstParsed);
        Assert.AreEqual("h", url.Host);
        Assert.AreEqual(81, url.Port);
        Assert.AreEqual("/p", url.AbsolutePath);
    }

    [TestMethod]
    public void TryParse_OnManyTasksAtOnce_GivesEachTheAnswerASingleCallGives()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        string[] texts =
        [
            "http://u:p@[fe80::1%25eth0]:8080/a/../b?q#f",
            "http://0x7f.1/",
            "http://h:65536/",
            "HTTP://H/./x",
        ];
        diagnostics.Arrange("texts", string.Join(" ", texts));
        (bool Parsed, CurlUrl? Url, CurlUrlRejection Rejection)[] expected = texts.Select(ParseOnce).ToArray();

        (bool Parsed, CurlUrl? Url, CurlUrlRejection Rejection)[] actual = Enumerable.Range(0, 400)
            .AsParallel()
            .Select(index => ParseOnce(texts[index % texts.Length]))
            .ToArray();

        int differences = actual
            .Select((answer, index) => (answer, index))
            .Count(pair => pair.answer != expected[pair.index % texts.Length]);
        diagnostics.Act("calls", actual.Length);
        diagnostics.Assert("answers differing from a single call", 0, differences);
        Assert.AreEqual(0, differences);
    }

    private static (bool Parsed, CurlUrl? Url, CurlUrlRejection Rejection) ParseOnce(string text)
    {
        bool parsed = CurlUrl.TryParse(text, pathAsIs: false, out CurlUrl? url, out CurlUrlRejection rejection);
        return (parsed, url, rejection);
    }

    private static CurlUrl AssertAccepted(TestDiagnostics diagnostics, string text)
    {
        diagnostics.Arrange("url text", text);

        bool parsed = CurlUrl.TryParse(text, pathAsIs: false, out CurlUrl? url, out CurlUrlRejection rejection);

        diagnostics.Act("parsed", parsed);
        diagnostics.Act("rejection", rejection);
        Assert.IsTrue(parsed, text);
        Assert.AreEqual(CurlUrlRejection.None, rejection);
        return url!;
    }

    private static void AssertRejected(TestDiagnostics diagnostics, string text, CurlUrlRejection expected)
    {
        diagnostics.Arrange("url text", text);

        bool parsed = CurlUrl.TryParse(text, pathAsIs: false, out CurlUrl? url, out CurlUrlRejection rejection);

        diagnostics.Act("parsed", parsed);
        diagnostics.Assert("rejection", expected, rejection);
        Assert.IsFalse(parsed, text);
        Assert.IsNull(url);
        Assert.AreEqual(expected, rejection, text);
    }
}
