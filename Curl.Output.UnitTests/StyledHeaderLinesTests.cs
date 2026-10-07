using System.Text;

using Curl.Testing;

namespace Curl.Output;

/// <summary>
/// Pins <see cref="StyledHeaderLines" /> against curl's bytes for <c>curl -si</c> on a
/// terminal, measured with curl 8.18.0 on Linux under <c>script</c> on 2026-09-29 (BL-736
/// Notes); the Windows bytes are curl-8_21_0's <c>src/tool_cb_hdr.c</c>.
/// </summary>
[TestClass]
public sealed class StyledHeaderLinesTests
{
    private const string TransferUrl = "http://127.0.0.1:8099/a";

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void Style_OkHeadOffWindows_BoldsEachNameAndEndsBoldWithAllAttributesOff()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("transfer URL", TransferUrl);
        diagnostics.Arrange("runs on Windows", false);
        StyledHeaderLines styles = StyledHeaderLines.ForPlatform(runsOnWindows: false, TransferUrl, vteVersion: null);

        string styled = StyleAll(styles, "HTTP/1.1 200 OK\r\n", "Content-Type: text/plain\r\n", "Content-Length: 3\r\n", "\r\n");

        string expected = "HTTP/1.1 200 OK\r\n\e[1mContent-Type\e[0m: text/plain\r\n\e[1mContent-Length\e[0m: 3\r\n\r\n";
        Report(diagnostics, expected, styled);
        Assert.AreEqual(expected, styled);
    }

    [TestMethod]
    public void Style_MovedHeadOffWindows_LinksTheLocationToTheUrlItResolvesTo()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("transfer URL", TransferUrl);
        diagnostics.Arrange("runs on Windows", false);
        StyledHeaderLines styles = StyledHeaderLines.ForPlatform(runsOnWindows: false, TransferUrl, vteVersion: null);

        string styled = StyleAll(styles, "HTTP/1.1 301 Moved Permanently\r\n", "Location: /next\r\n", "Content-Length: 0\r\n", "\r\n");

        string expected = "HTTP/1.1 301 Moved Permanently\r\n"
            + "\e[1mLocation\e[0m: \e]8;;http://127.0.0.1:8099/next\e\\/next\r\n\e]8;;\e\\"
            + "\e[1mContent-Length\e[0m: 0\r\n\r\n";
        Report(diagnostics, expected, styled);
        Assert.AreEqual(expected, styled);
    }

    [TestMethod]
    public void Style_OnWindows_EndsBoldWithBoldOffAndLinksNothing()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("transfer URL", TransferUrl);
        diagnostics.Arrange("runs on Windows", true);
        StyledHeaderLines styles = StyledHeaderLines.ForPlatform(runsOnWindows: true, TransferUrl, vteVersion: null);

        string styled = StyleAll(styles, "Location: /next\r\n", "Content-Length: 0\r\n");

        string expected = "\e[1mLocation\e[22m: /next\r\n\e[1mContent-Length\e[22m: 0\r\n";
        Report(diagnostics, expected, styled);
        Assert.AreEqual(expected, styled);
    }

    [TestMethod]
    public void Style_LocationWithAnUnlinkedScheme_WritesTheValueAsItIs()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("header line", "Location:  mailto:x@y");
        StyledHeaderLines styles = StyledHeaderLines.ForPlatform(runsOnWindows: false, TransferUrl, vteVersion: null);

        string styled = StyleAll(styles, "Location:  mailto:x@y\r\n");

        string expected = "\e[1mLocation\e[0m:  mailto:x@y\r\n";
        Report(diagnostics, expected, styled);
        Assert.AreEqual(expected, styled);
    }

    [TestMethod]
    [DataRow("Location:\r\n")]
    [DataRow("Location: /a b\r\n")]
    [DataRow("Location: /a\x7f\r\n")]
    [DataRow("Location: http://[::1\r\n")]
    public void Style_LocationThatIsNoUrl_WritesTheValueAsItIs(string line)
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("header line", line);
        StyledHeaderLines styles = StyledHeaderLines.ForPlatform(runsOnWindows: false, TransferUrl, vteVersion: null);

        string styled = StyleAll(styles, line);

        string expected = "\e[1mLocation\e[0m" + line["Location".Length..];
        Report(diagnostics, expected, styled);
        Assert.AreEqual(expected, styled);
    }

    [TestMethod]
    public void Style_TransferUrlThatIsNoUrl_WritesARelativeLocationAsItIs()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("transfer URL", "not a url");
        StyledHeaderLines styles = StyledHeaderLines.ForPlatform(runsOnWindows: false, "not a url", vteVersion: null);

        string styled = StyleAll(styles, "Location: /next\r\n");

        string expected = "\e[1mLocation\e[0m: /next\r\n";
        Report(diagnostics, expected, styled);
        Assert.AreEqual(expected, styled);
    }

    [TestMethod]
    public void Style_TabBeforeAnAbsoluteLocation_KeepsTheBlanksOutsideTheLink()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("header line", "location:<TAB> https://example.com/x");
        StyledHeaderLines styles = StyledHeaderLines.ForPlatform(runsOnWindows: false, TransferUrl, vteVersion: null);

        string styled = StyleAll(styles, "location:\t https://example.com/x\n");

        string expected = "\e[1mlocation\e[0m:\t \e]8;;https://example.com/x\e\\https://example.com/x\n\e]8;;\e\\";
        Report(diagnostics, expected, styled);
        Assert.AreEqual(expected, styled);
    }

    [TestMethod]
    public void Style_SecondLocation_ResolvesAgainstTheFirst()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("header lines", "Location: http://example.com/d/x, Location: y");
        StyledHeaderLines styles = StyledHeaderLines.ForPlatform(runsOnWindows: false, TransferUrl, vteVersion: null);

        string styled = StyleAll(styles, "Location: http://example.com/d/x\r\n", "Location: y\r\n");

        string expectedSuffix = "\e[1mLocation\e[0m: \e]8;;http://example.com/d/y\e\\y\r\n\e]8;;\e\\";
        diagnostics.Act("styled", styled);
        diagnostics.Assert("ends with", expectedSuffix, styled.Length >= expectedSuffix.Length ? styled[^expectedSuffix.Length..] : styled);
        Assert.EndsWith(expectedSuffix, styled);
    }

    [TestMethod]
    [DataRow("Loc: /next\r\n", "Loc")]
    [DataRow(": /next\r\n", "")]
    public void Style_NameThatPrefixesLocation_IsLinkedAsCurlsPrefixCompareDoes(string line, string name)
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("header line", line);
        diagnostics.Arrange("name", name);
        StyledHeaderLines styles = StyledHeaderLines.ForPlatform(runsOnWindows: false, TransferUrl, vteVersion: null);

        string styled = StyleAll(styles, line);

        string expected = "\e[1m" + name + "\e[0m: \e]8;;http://127.0.0.1:8099/next\e\\/next\r\n\e]8;;\e\\";
        Report(diagnostics, expected, styled);
        Assert.AreEqual(expected, styled);
    }

    [TestMethod]
    [DataRow("Location-X: /next\r\n", "Location-X")]
    [DataRow("Lox: /next\r\n", "Lox")]
    public void Style_NameThatIsNotAPrefixOfLocation_IsNotLinked(string line, string name)
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("header line", line);
        diagnostics.Arrange("name", name);
        StyledHeaderLines styles = StyledHeaderLines.ForPlatform(runsOnWindows: false, TransferUrl, vteVersion: null);

        string styled = StyleAll(styles, line);

        string expected = "\e[1m" + name + "\e[0m: /next\r\n";
        Report(diagnostics, expected, styled);
        Assert.AreEqual(expected, styled);
    }

    [TestMethod]
    [DataRow("4801")]
    [DataRow("3800abc")]
    public void Style_InsideVteUpTo0481_LinksNothing(string vteVersion)
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("VTE version", vteVersion);
        StyledHeaderLines styles = StyledHeaderLines.ForPlatform(runsOnWindows: false, TransferUrl, vteVersion);

        string styled = StyleAll(styles, "Location: /next\r\n");

        string expected = "\e[1mLocation\e[0m: /next\r\n";
        Report(diagnostics, expected, styled);
        Assert.AreEqual(expected, styled);
    }

    [TestMethod]
    [DataRow("4802")]
    [DataRow("")]
    [DataRow("x4000")]
    [DataRow("99999999999999999999")]
    public void Style_InsideNewerOrUnreadableVte_LinksTheLocation(string vteVersion)
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("VTE version", vteVersion);
        StyledHeaderLines styles = StyledHeaderLines.ForPlatform(runsOnWindows: false, TransferUrl, vteVersion);

        string styled = StyleAll(styles, "Location: /next\r\n");

        string expectedPrefix = "\e[1mLocation\e[0m: \e]8;;";
        diagnostics.Act("styled", styled);
        diagnostics.Assert("starts with", expectedPrefix, styled.Length >= expectedPrefix.Length ? styled[..expectedPrefix.Length] : styled);
        Assert.StartsWith(expectedPrefix, styled);
    }

    [TestMethod]
    public void ForPlatform_NullTransferUrl_Throws()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("transfer URL", "null");

        ArgumentNullException exception = Assert.ThrowsExactly<ArgumentNullException>(() => StyledHeaderLines.ForPlatform(runsOnWindows: false, null!, vteVersion: null));

        diagnostics.Act("exception type", exception.GetType().Name);
        diagnostics.Act("exception message", exception.Message);
        diagnostics.Assert("exception type", nameof(ArgumentNullException), exception.GetType().Name);
    }

    private static void Report(TestDiagnostics diagnostics, string expected, string styled)
    {
        diagnostics.Act("styled", styled);
        diagnostics.Diff("styled", expected, styled);
    }

    private static string StyleAll(StyledHeaderLines styles, params string[] lines) =>
        string.Concat(lines.Select(line => Encoding.Latin1.GetString(styles.Style(Encoding.Latin1.GetBytes(line)))));
}
