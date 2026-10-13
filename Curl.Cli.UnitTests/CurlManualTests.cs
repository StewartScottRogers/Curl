using System.Security.Cryptography;
using System.Text;
using Curl.Testing;

namespace Curl.Cli;

/// <summary>
/// Pins <see cref="CurlManual"/> to curl 8.22.0's <c>-M</c>: <c>/ucrt64/bin/curl -M &gt; m.out</c> on
/// Windows on 2026-10-13 wrote 305475 bytes, 7983 lines each ended with CR LF, whose SHA-256 is below.
/// <c>--manual</c> wrote the same bytes, and <c>COLUMNS=40</c> did not change them.
/// </summary>
[TestClass]
public sealed class CurlManualTests
{
    private const string MeasuredSha256 = "dd2789bd5a5462bb2dfb7cf9058d22ad82496584c213003f3b18adf02b9a61e7";

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public void Lines_JoinedWithCrLf_AreTheMeasuredBytes()
    {
        Diagnostics.Arrange("line terminator", "CR LF");
        IReadOnlyList<string> lines = Lines();
        byte[] bytes = Encoding.ASCII.GetBytes(string.Concat(lines.Select(line => line + "\r\n")));
        string sha256 = Convert.ToHexStringLower(SHA256.HashData(bytes));
        Diagnostics.Bytes("manual", bytes);
        Diagnostics.Act("sha-256", sha256);

        Diagnostics.Assert("line count", 7983, lines.Count);
        Diagnostics.Assert("byte count", 305475, bytes.Length);
        Diagnostics.Assert("sha-256", MeasuredSha256, sha256);
        Assert.HasCount(7983, lines);
        Assert.AreEqual(305475, bytes.Length);
        Assert.AreEqual(MeasuredSha256, Convert.ToHexStringLower(SHA256.HashData(bytes)));
    }

    [TestMethod]
    public void Lines_StartWithTheBannerAndEndWithABlankLine()
    {
        Diagnostics.Arrange("lines checked", "first, sixth and last");
        IReadOnlyList<string> lines = Lines();

        Diagnostics.Assert("first line", "\"          _   _ ____  _\"", "\"" + lines[0] + "\"");
        Diagnostics.Assert("sixth line", "NAME", lines[5]);
        Diagnostics.Assert("last line", "\"\"", "\"" + lines[^1] + "\"");
        Assert.AreEqual("          _   _ ____  _", lines[0]);
        Assert.AreEqual("NAME", lines[5]);
        Assert.AreEqual(string.Empty, lines[^1]);
    }

    /// <summary>Returns <see cref="CurlManual.Lines"/>, writing how many there are as a diagnostic.</summary>
    private IReadOnlyList<string> Lines()
    {
        IReadOnlyList<string> lines = CurlManual.Lines();
        Diagnostics.Act("line count", lines.Count);
        return lines;
    }
}
