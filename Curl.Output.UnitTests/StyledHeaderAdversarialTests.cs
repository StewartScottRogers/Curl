using System.Text;

namespace Curl.Output;

/// <summary>
/// Adversarial black-box tests for <see cref="StyledHeaderLines"/> and
/// <see cref="StyledHeaderStream"/> (BL-1504): empty and colon-only lines, terminal escapes
/// smuggled into a header or a <c>Location</c>, schemes that must not be linked, and writes
/// split at awkward places.
/// </summary>
[TestClass]
public sealed class StyledHeaderAdversarialTests
{
    private const string BoldOn = "\e[1m";
    private const string UnixBoldOff = "\e[0m";

    [TestMethod]
    public void Style_EmptyLine_IsReturnedEmpty()
    {
        StyledHeaderLines styles = StyledHeaderLines.ForPlatform(runsOnWindows: false, "http://example.com/", vteVersion: null);

        byte[] styled = styles.Style([]);

        Assert.IsEmpty(styled);
    }

    [TestMethod]
    public void Style_LineOfOnlyAColon_BoldsAnEmptyName()
    {
        StyledHeaderLines styles = StyledHeaderLines.ForPlatform(runsOnWindows: true, "http://example.com/", vteVersion: null);

        byte[] styled = styles.Style(":"u8);

        Assert.AreEqual(BoldOn + "\e[22m:", Encoding.Latin1.GetString(styled));
    }

    [TestMethod]
    public void Style_EscapeSequenceInHeaderValue_IsPassedThroughAsCurlDoes()
    {
        StyledHeaderLines styles = StyledHeaderLines.ForPlatform(runsOnWindows: false, "http://example.com/", vteVersion: null);

        byte[] styled = styles.Style("X: \e[31mred\r\n"u8);

        Assert.AreEqual(BoldOn + "X" + UnixBoldOff + ": \e[31mred\r\n", Encoding.Latin1.GetString(styled));
    }

    [TestMethod]
    [DataRow("Location: http://evil/\e]8;;http://x\e\\\r\n")]
    [DataRow("Location: http://a/\x7f\r\n")]
    [DataRow("Location: javascript:alert(1)\r\n")]
    [DataRow("Location: file:///etc/passwd\r\n")]
    [DataRow("Location: \r\n")]
    [DataRow("Location:\r\n")]
    public void Style_LocationWithControlBytesOrAnUnlinkedScheme_IsNotLinked(string line)
    {
        StyledHeaderLines styles = StyledHeaderLines.ForPlatform(runsOnWindows: false, "http://example.com/", vteVersion: null);

        string styled = Encoding.Latin1.GetString(styles.Style(Encoding.Latin1.GetBytes(line)));

        Assert.AreEqual(BoldOn + "Location" + UnixBoldOff + line["Location".Length..], styled);
    }

    [TestMethod]
    public void Style_LocationWithHighLatin1Bytes_LinksWithoutThrowing()
    {
        StyledHeaderLines styles = StyledHeaderLines.ForPlatform(runsOnWindows: false, "http://example.com/", vteVersion: null);

        byte[] styled = styles.Style(Encoding.Latin1.GetBytes("Location: /ÿé\r\n"));

        Assert.Contains("\e]8;;http://example.com/", Encoding.Latin1.GetString(styled));
    }

    [TestMethod]
    public void Style_UnparsableBaseUrl_LeavesTheLocationUnlinked()
    {
        StyledHeaderLines styles = StyledHeaderLines.ForPlatform(runsOnWindows: false, "not a url", vteVersion: null);

        string styled = Encoding.Latin1.GetString(styles.Style("Location: /next\r\n"u8));

        Assert.AreEqual(BoldOn + "Location" + UnixBoldOff + ": /next\r\n", styled);
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("abc")]
    [DataRow("-1")]
    [DataRow("99999999999999999999999999")]
    public void ForPlatform_VteVersionThatIsNotAnOldVersionNumber_StillLinksLocation(string vteVersion)
    {
        StyledHeaderLines styles = StyledHeaderLines.ForPlatform(runsOnWindows: false, "http://example.com/", vteVersion);

        string styled = Encoding.Latin1.GetString(styles.Style("Location: /n\r\n"u8));

        Assert.Contains("\e]8;;http://example.com/n\e\\", styled);
    }

    [TestMethod]
    public void Style_ManyRedirectsInARow_EachResolvesAgainstThePreviousLocation()
    {
        StyledHeaderLines styles = StyledHeaderLines.ForPlatform(runsOnWindows: false, "http://example.com/", vteVersion: null);
        string last = string.Empty;

        for (int index = 0; index < 1000; index++)
        {
            last = Encoding.Latin1.GetString(styles.Style("Location: a/\r\n"u8));
        }

        Assert.Contains("\e]8;;http://example.com/" + string.Concat(Enumerable.Repeat("a/", 1000)) + "\e\\", last);
    }

    [TestMethod]
    public void Write_ManyLinesInOneWrite_StylesEachLine()
    {
        StyledHeaderLines styles = StyledHeaderLines.ForPlatform(runsOnWindows: true, "http://example.com/", vteVersion: null);
        using MemoryStream output = new();
        using StyledHeaderStream stream = new(output, styles);

        stream.Write(Encoding.Latin1.GetBytes(string.Concat(Enumerable.Repeat("A: 1\r\n", 1000))));

        Assert.AreEqual(
            string.Concat(Enumerable.Repeat(BoldOn + "A\e[22m: 1\r\n", 1000)),
            Encoding.Latin1.GetString(output.ToArray()));
    }

    [TestMethod]
    public void Write_LineFeedAloneAndBareLineFeeds_PassThroughUnstyled()
    {
        StyledHeaderLines styles = StyledHeaderLines.ForPlatform(runsOnWindows: true, "http://example.com/", vteVersion: null);
        using MemoryStream output = new();
        using StyledHeaderStream stream = new(output, styles);

        stream.Write("\n\r\n\n"u8);

        Assert.AreEqual("\n\r\n\n", Encoding.Latin1.GetString(output.ToArray()));
    }

    [TestMethod]
    public void Write_EmptyBuffer_WritesNothing()
    {
        StyledHeaderLines styles = StyledHeaderLines.ForPlatform(runsOnWindows: true, "http://example.com/", vteVersion: null);
        using MemoryStream output = new();
        using StyledHeaderStream stream = new(output, styles);

        stream.Write([], 0, 0);

        Assert.AreEqual(0, output.Length);
    }

    [TestMethod]
    public void ReadSeekLengthPosition_OnAWriteOnlyStream_ThrowNotSupported()
    {
        StyledHeaderLines styles = StyledHeaderLines.ForPlatform(runsOnWindows: true, "http://example.com/", vteVersion: null);
        using StyledHeaderStream stream = new(Stream.Null, styles);

        Assert.ThrowsExactly<NotSupportedException>(() => stream.Read(new byte[1], 0, 1));
        Assert.ThrowsExactly<NotSupportedException>(() => stream.Seek(0, SeekOrigin.Begin));
        Assert.ThrowsExactly<NotSupportedException>(() => stream.SetLength(0));
        Assert.ThrowsExactly<NotSupportedException>(() => stream.Length);
        Assert.ThrowsExactly<NotSupportedException>(() => stream.Position);
        Assert.ThrowsExactly<NotSupportedException>(() => stream.Position = 0);
    }
}
