using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Original.Protocol.Gopher.Fakes;

namespace Curl.Original.Protocol.Gopher;

/// <summary>
/// Pins CGR-105 (BL-3634): under <c>--path-as-is</c> a gopher selector keeps the path's dot
/// segments, and without it they are still removed. Real curl 8.22.0 on Windows was measured
/// with <c>Record-CurlExchange.ps1</c> on 2026-10-26 (<c>curl -s -S --path-as-is
/// gopher://127.0.0.1:PORT/0/a/../x</c> sends <c>/a/../x</c> and CRLF); the cases hold only
/// ASCII, so they run the same on every platform.
/// </summary>
[TestClass]
public sealed class Cgr105Tests
{
    [TestMethod]
    [DataRow("gopher://127.0.0.1:21001/0/a/../x", "/a/../x")]
    [DataRow("gopher://127.0.0.1:21001/0/../x", "/../x")]
    [DataRow("gopher://127.0.0.1:21001/0/./x", "/./x")]
    [DataRow("gopher://127.0.0.1:21001/1/../../a", "/../../a")]
    [DataRow("gopher://127.0.0.1:21001/../x", "./x")]
    public void FromUrl_PathAsIs_KeepsDotSegments(string url, string expected)
    {
        byte[]? actual = GopherSelector.FromUrl(CurlUrl.Parse(url), pathAsIs: true);

        Assert.AreEqual(expected, Encoding.ASCII.GetString(actual!));
    }

    [TestMethod]
    [DataRow("gopher://127.0.0.1:21001/0/a/../x", "/x")]
    [DataRow("gopher://127.0.0.1:21001/0/../x", "")]
    public void FromUrl_NotPathAsIs_RemovesDotSegments(string url, string expected)
    {
        byte[]? actual = GopherSelector.FromUrl(CurlUrl.Parse(url), pathAsIs: false);

        Assert.AreEqual(expected, Encoding.ASCII.GetString(actual!));
    }

    [TestMethod]
    public async Task ExecuteAsync_PathAsIs_SendsTheSelectorWithItsDotSegments()
    {
        ScriptedConnection connection = new("hi\r\n"u8.ToArray());
        var context = new TransferContext
        {
            Url = CurlUrl.Parse("gopher://127.0.0.1:21001/0/a/../x"),
            Output = new MemoryStream(),
            PathAsIs = true,
        };

        TransferResult result = await new GopherProtocolHandler(FakeConnector.For(connection)).ExecuteAsync(context);

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual("/a/../x\r\n", Encoding.ASCII.GetString(connection.Written));
    }
}
