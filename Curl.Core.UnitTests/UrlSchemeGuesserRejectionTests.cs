using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Core;

/// <summary>
/// Pins that a URL typed without a scheme whose authority ends in an empty port is left without
/// one, so the transfer URL parser rejects it as curl 8.22.0 does: exit 3,
/// <c>curl: (3) URL rejected: Port number was not a decimal number between 0 and 65535</c>
/// (measured with <c>Record-CurlExchange.ps1</c> on Windows, 2026-10-22, BL-3175 Notes).
/// </summary>
[TestClass]
public sealed class UrlSchemeGuesserRejectionTests
{
    private const string BadPortMessage = "Port number was not a decimal number between 0 and 65535";

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [DataRow("http:")]
    [DataRow("1http://127.0.0.1:9/")]
    [DataRow("1http://x/")]
    [DataRow("127.0.0.1:")]
    [DataRow("127.0.0.1:/")]
    [DataRow("u@127.0.0.1:/x")]
    public void AddScheme_EmptyGuessedPort_IsRejectedAsABadPort(string url)
    {
        AssertRejectedAsABadPort(url);
    }

    // On Windows "h:" is a drive prefix, not a scheme, so the authority is "h:" with an empty port.
    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    [DataRow("h://127.0.0.1:9/x")]
    [DataRow("h://x/")]
    public void AddScheme_DriveLetterSchemeOnWindows_IsRejectedAsABadPort(string url)
    {
        AssertRejectedAsABadPort(url);
    }

    [TestMethod]
    public void AddScheme_EmptyPortWithAProtoDefault_LeavesTheUrlWithoutAScheme()
    {
        string withScheme = UrlSchemeGuesser.AddScheme("http:", "ftp");

        Assert.AreEqual("http:", withScheme);
    }

    // A file URL has no authority to hold a port: --proto-default file keeps the drive-like prefix (test1146).
    [TestMethod]
    public void AddScheme_EmptyPortWithProtoDefaultFile_AddsTheFileScheme()
    {
        string withScheme = UrlSchemeGuesser.AddScheme("dir:", "file");

        Assert.AreEqual("file://dir:", withScheme);
    }

    [TestMethod]
    [DataRow("127.0.0.1:9/", "http://127.0.0.1:9/")]
    [DataRow("ftp.x:21", "ftp://ftp.x:21")]
    public void AddScheme_PortNotEmpty_AddsTheGuessedScheme(string url, string expected)
    {
        Assert.AreEqual(expected, UrlSchemeGuesser.AddScheme(url, null));
    }

    [TestMethod]
    public void AddScheme_SchemeGivenWithEmptyPort_KeepsTheUrlAndParses()
    {
        // curl -sS http://127.0.0.1:/ -> connects to port 80 (exit 7): an empty port is fine with a scheme.
        string withScheme = UrlSchemeGuesser.AddScheme("http://127.0.0.1:/", null);

        bool parsed = CurlUrl.TryParse(withScheme, pathAsIs: false, out CurlUrl? url, out _);

        Assert.IsTrue(parsed);
        Assert.AreEqual(80, url!.Port);
    }

    [TestMethod]
    public void AddScheme_SpaceInTheScheme_StaysMalformedInput()
    {
        // curl -sS "ht tp://x/" -> curl: (3) URL rejected: Malformed input to a URL function.
        string withScheme = UrlSchemeGuesser.AddScheme("ht tp://x/", null);

        bool parsed = CurlUrl.TryParse(withScheme, pathAsIs: false, out _, out CurlUrlRejection rejection);

        Assert.IsFalse(parsed);
        Assert.AreEqual("Malformed input to a URL function", rejection.ToCurlMessage());
    }

    private void AssertRejectedAsABadPort(string url)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("url", url);

        string withScheme = UrlSchemeGuesser.AddScheme(url, null);
        bool parsed = CurlUrl.TryParse(withScheme, pathAsIs: false, out _, out CurlUrlRejection rejection);

        diagnostics.Act("with scheme", withScheme);
        diagnostics.Act("rejection", rejection);
        Assert.AreEqual(url, withScheme);
        Assert.IsFalse(parsed);
        Assert.AreEqual(BadPortMessage, rejection.ToCurlMessage());
    }
}
