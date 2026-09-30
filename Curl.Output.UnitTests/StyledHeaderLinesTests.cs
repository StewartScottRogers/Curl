using System.Text;

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

    [TestMethod]
    public void Style_OkHeadOffWindows_BoldsEachNameAndEndsBoldWithAllAttributesOff()
    {
        StyledHeaderLines styles = StyledHeaderLines.ForPlatform(runsOnWindows: false, TransferUrl, vteVersion: null);

        string styled = StyleAll(styles, "HTTP/1.1 200 OK\r\n", "Content-Type: text/plain\r\n", "Content-Length: 3\r\n", "\r\n");

        Assert.AreEqual(
            "HTTP/1.1 200 OK\r\n\e[1mContent-Type\e[0m: text/plain\r\n\e[1mContent-Length\e[0m: 3\r\n\r\n",
            styled);
    }

    [TestMethod]
    public void Style_MovedHeadOffWindows_LinksTheLocationToTheUrlItResolvesTo()
    {
        StyledHeaderLines styles = StyledHeaderLines.ForPlatform(runsOnWindows: false, TransferUrl, vteVersion: null);

        string styled = StyleAll(styles, "HTTP/1.1 301 Moved Permanently\r\n", "Location: /next\r\n", "Content-Length: 0\r\n", "\r\n");

        Assert.AreEqual(
            "HTTP/1.1 301 Moved Permanently\r\n"
            + "\e[1mLocation\e[0m: \e]8;;http://127.0.0.1:8099/next\e\\/next\r\n\e]8;;\e\\"
            + "\e[1mContent-Length\e[0m: 0\r\n\r\n",
            styled);
    }

    [TestMethod]
    public void Style_OnWindows_EndsBoldWithBoldOffAndLinksNothing()
    {
        StyledHeaderLines styles = StyledHeaderLines.ForPlatform(runsOnWindows: true, TransferUrl, vteVersion: null);

        string styled = StyleAll(styles, "Location: /next\r\n", "Content-Length: 0\r\n");

        Assert.AreEqual("\e[1mLocation\e[22m: /next\r\n\e[1mContent-Length\e[22m: 0\r\n", styled);
    }

    [TestMethod]
    public void Style_LocationWithAnUnlinkedScheme_WritesTheValueAsItIs()
    {
        StyledHeaderLines styles = StyledHeaderLines.ForPlatform(runsOnWindows: false, TransferUrl, vteVersion: null);

        Assert.AreEqual("\e[1mLocation\e[0m:  mailto:x@y\r\n", StyleAll(styles, "Location:  mailto:x@y\r\n"));
    }

    [TestMethod]
    [DataRow("Location:\r\n")]
    [DataRow("Location: /a b\r\n")]
    [DataRow("Location: /a\x7f\r\n")]
    [DataRow("Location: http://[::1\r\n")]
    public void Style_LocationThatIsNoUrl_WritesTheValueAsItIs(string line)
    {
        StyledHeaderLines styles = StyledHeaderLines.ForPlatform(runsOnWindows: false, TransferUrl, vteVersion: null);

        Assert.AreEqual("\e[1mLocation\e[0m" + line["Location".Length..], StyleAll(styles, line));
    }

    [TestMethod]
    public void Style_TransferUrlThatIsNoUrl_WritesARelativeLocationAsItIs()
    {
        StyledHeaderLines styles = StyledHeaderLines.ForPlatform(runsOnWindows: false, "not a url", vteVersion: null);

        Assert.AreEqual("\e[1mLocation\e[0m: /next\r\n", StyleAll(styles, "Location: /next\r\n"));
    }

    [TestMethod]
    public void Style_TabBeforeAnAbsoluteLocation_KeepsTheBlanksOutsideTheLink()
    {
        StyledHeaderLines styles = StyledHeaderLines.ForPlatform(runsOnWindows: false, TransferUrl, vteVersion: null);

        Assert.AreEqual(
            "\e[1mlocation\e[0m:\t \e]8;;https://example.com/x\e\\https://example.com/x\n\e]8;;\e\\",
            StyleAll(styles, "location:\t https://example.com/x\n"));
    }

    [TestMethod]
    public void Style_SecondLocation_ResolvesAgainstTheFirst()
    {
        StyledHeaderLines styles = StyledHeaderLines.ForPlatform(runsOnWindows: false, TransferUrl, vteVersion: null);

        string styled = StyleAll(styles, "Location: http://example.com/d/x\r\n", "Location: y\r\n");

        Assert.EndsWith("\e[1mLocation\e[0m: \e]8;;http://example.com/d/y\e\\y\r\n\e]8;;\e\\", styled);
    }

    [TestMethod]
    [DataRow("Loc: /next\r\n", "Loc")]
    [DataRow(": /next\r\n", "")]
    public void Style_NameThatPrefixesLocation_IsLinkedAsCurlsPrefixCompareDoes(string line, string name)
    {
        StyledHeaderLines styles = StyledHeaderLines.ForPlatform(runsOnWindows: false, TransferUrl, vteVersion: null);

        Assert.AreEqual(
            "\e[1m" + name + "\e[0m: \e]8;;http://127.0.0.1:8099/next\e\\/next\r\n\e]8;;\e\\",
            StyleAll(styles, line));
    }

    [TestMethod]
    [DataRow("Location-X: /next\r\n", "Location-X")]
    [DataRow("Lox: /next\r\n", "Lox")]
    public void Style_NameThatIsNotAPrefixOfLocation_IsNotLinked(string line, string name)
    {
        StyledHeaderLines styles = StyledHeaderLines.ForPlatform(runsOnWindows: false, TransferUrl, vteVersion: null);

        Assert.AreEqual("\e[1m" + name + "\e[0m: /next\r\n", StyleAll(styles, line));
    }

    [TestMethod]
    [DataRow("4801")]
    [DataRow("3800abc")]
    public void Style_InsideVteUpTo0481_LinksNothing(string vteVersion)
    {
        StyledHeaderLines styles = StyledHeaderLines.ForPlatform(runsOnWindows: false, TransferUrl, vteVersion);

        Assert.AreEqual("\e[1mLocation\e[0m: /next\r\n", StyleAll(styles, "Location: /next\r\n"));
    }

    [TestMethod]
    [DataRow("4802")]
    [DataRow("")]
    [DataRow("x4000")]
    [DataRow("99999999999999999999")]
    public void Style_InsideNewerOrUnreadableVte_LinksTheLocation(string vteVersion)
    {
        StyledHeaderLines styles = StyledHeaderLines.ForPlatform(runsOnWindows: false, TransferUrl, vteVersion);

        Assert.StartsWith("\e[1mLocation\e[0m: \e]8;;", StyleAll(styles, "Location: /next\r\n"));
    }

    [TestMethod]
    public void ForPlatform_NullTransferUrl_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => StyledHeaderLines.ForPlatform(runsOnWindows: false, null!, vteVersion: null));
    }

    private static string StyleAll(StyledHeaderLines styles, params string[] lines) =>
        string.Concat(lines.Select(line => Encoding.Latin1.GetString(styles.Style(Encoding.Latin1.GetBytes(line)))));
}
