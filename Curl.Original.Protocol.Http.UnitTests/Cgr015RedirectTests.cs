using System.Text;
using Curl.Protocol.Abstractions;

namespace Curl.Original.Protocol.Http;

/// <summary>
/// Pins CGR-015 part 3b (BL-3891): curl 8.22.0 on Windows percent-encodes the command-line URL's
/// path from its ANSI <c>argv</c> bytes, so <c>-L</c> from <c>http://h/é/x</c> to
/// <c>Location: y</c> sends <c>GET /%E9/y</c> (measured with Record-CurlExchange.ps1, Notes). The
/// encoding is passed in, so the tests read the same on every platform; UTF-8 keeps the bytes
/// sent before.
/// </summary>
[TestClass]
public sealed class Cgr015RedirectTests
{
    private static readonly Encoding Cp1252 = CodePagesEncodingProvider.Instance.GetEncoding(1252)!;

    [TestMethod]
    public void Resolve_RelativeLocationWithCp1252_KeepsThePathEncodedFromItsBytes()
    {
        string resolved = HttpRedirectLocation.Resolve(CurlUrl.Parse("http://127.0.0.1:21893/é/x"), "y", Cp1252, driveLetters: false);

        Assert.AreEqual("http://127.0.0.1:21893/%E9/y", resolved);
    }

    [TestMethod]
    public void Resolve_RelativeLocationWithUtf8_KeepsTheUtf8PathAsBefore()
    {
        string resolved = HttpRedirectLocation.Resolve(CurlUrl.Parse("http://127.0.0.1:21893/é/x"), "y", Encoding.UTF8, driveLetters: false);

        Assert.AreEqual("http://127.0.0.1:21893/%C3%A9/y", resolved);
    }

    [TestMethod]
    public void IssueAnotherRequest_Cp1252_PrintsThePathEncodedFromItsBytes()
    {
        string line = HttpConnectionInfoLines.IssueAnotherRequest(CurlUrl.Parse("http://127.0.0.1:21893/é/x"), Cp1252);

        Assert.AreEqual("Issue another request to this URL: 'http://127.0.0.1:21893/%E9/x'", line);
    }

    [TestMethod]
    public void IssueAnotherRequest_Utf8_PrintsTheUtf8PathAsBefore()
    {
        string line = HttpConnectionInfoLines.IssueAnotherRequest(CurlUrl.Parse("http://127.0.0.1:21893/é/x"), Encoding.UTF8);

        Assert.AreEqual("Issue another request to this URL: 'http://127.0.0.1:21893/%C3%A9/x'", line);
    }
}
