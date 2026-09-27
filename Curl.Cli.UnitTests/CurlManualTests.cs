using System.Security.Cryptography;
using System.Text;

namespace Curl.Cli;

/// <summary>
/// Pins <see cref="CurlManual"/> to curl 8.21.0's <c>-M</c>: <c>/mingw64/bin/curl -M &gt; m.out</c> on
/// Windows on 2026-09-27 wrote 299744 bytes, 7849 lines each ended with CR LF, whose SHA-256 is below.
/// <c>--manual</c> wrote the same bytes, and <c>COLUMNS=40</c> did not change them.
/// </summary>
[TestClass]
public sealed class CurlManualTests
{
    private const string MeasuredSha256 = "b283726b16afd8394477299ce5780f7fcaf2043bc4d664d736a09ab591349e9f";

    [TestMethod]
    public void Lines_JoinedWithCrLf_AreTheMeasuredBytes()
    {
        IReadOnlyList<string> lines = CurlManual.Lines();
        byte[] bytes = Encoding.ASCII.GetBytes(string.Concat(lines.Select(line => line + "\r\n")));

        Assert.HasCount(7849, lines);
        Assert.AreEqual(299744, bytes.Length);
        Assert.AreEqual(MeasuredSha256, Convert.ToHexStringLower(SHA256.HashData(bytes)));
    }

    [TestMethod]
    public void Lines_StartWithTheBannerAndEndWithABlankLine()
    {
        IReadOnlyList<string> lines = CurlManual.Lines();

        Assert.AreEqual("          _   _ ____  _", lines[0]);
        Assert.AreEqual("NAME", lines[5]);
        Assert.AreEqual(string.Empty, lines[^1]);
    }
}
