using System.Text;
using Curl.Testing;

namespace Curl.Console;

/// <summary>
/// Pins the file name <see cref="RedirectLocationFileName" /> reads from a <c>Location</c> line,
/// the name curl 8.21.0 gives a <c>-J -L</c> file without <c>Content-Disposition</c> (BL-1809).
/// </summary>
[TestClass]
public sealed class RedirectLocationFileNameTests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    [DataRow("Location: /16420002\r\n", "16420002")]
    [DataRow("LOCATION:  dir/a%20b.txt?q=/x#f/y\r\n", "a%20b.txt")]
    [DataRow("location: http://host:8080/d/e.bin#frag", "e.bin")]
    [DataRow("Location: e.txt", "e.txt")]
    public void Find_LocationLine_GivesTheLastPathSegment(string line, string expected)
    {
        Diagnostics.Arrange("line", line.ReplaceLineEndings("\\n"));

        string? found = RedirectLocationFileName.Find(Encoding.ASCII.GetBytes(line));

        Diagnostics.Assert("file name", expected, found ?? "(none)");
        Assert.AreEqual(expected, found);
    }

    [TestMethod]
    [DataRow("Content-Location: /x.txt\r\n")]
    [DataRow("Location: /dir/\r\n")]
    [DataRow("Location: http://host\r\n")]
    [DataRow("Location: http://host?x=/y\r\n")]
    public void Find_NoFileName_GivesNone(string line)
    {
        Diagnostics.Arrange("line", line.ReplaceLineEndings("\\n"));

        string? found = RedirectLocationFileName.Find(Encoding.ASCII.GetBytes(line));

        Diagnostics.Assert("file name", "(none)", found ?? "(none)");
        Assert.IsNull(found);
    }
}
