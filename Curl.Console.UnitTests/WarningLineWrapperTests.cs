using Curl.Testing;

namespace Curl.Console;

/// <summary>
/// Pins warning wrapping against curl 8.21.0, measured on Windows on 2026-09-26 with
/// <c>curl --no-progress-meter -o C:/Windows/System32/bl087.txt file:///C:/Windows/win.ini</c>.
/// </summary>
[TestClass]
public sealed class WarningLineWrapperTests
{
    private const string MeasuredText = "Failed to open the file C:/Windows/System32/bl087.txt: Permission denied";

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public void WrapText_MeasuredWarningAtDefault79Columns_IsCurlsTwoLines()
    {
        IReadOnlyList<string> lines = WrapText(MeasuredText, 79);

        Expect(lines, "Warning: Failed to open the file C:/Windows/System32/bl087.txt: Permission ", "Warning: denied");
        CollectionAssert.AreEqual(
            new[]
            {
                "Warning: Failed to open the file C:/Windows/System32/bl087.txt: Permission ",
                "Warning: denied",
            },
            lines.ToArray());
    }

    [TestMethod]
    public void WrapNoteText_MeasuredConfigFileNoteAtDefault79Columns_IsCurlsTwoLines()
    {
        const string text = @"Read config file from 'C:\Users\Stewart Rogers\AppData\Local\Temp\bl243\.curlrc'";
        Diagnostics.Arrange("note text / columns", $"{text} / 79");
        IReadOnlyList<string> lines = WarningLineWrapper.WrapNoteText(
            @"Read config file from 'C:\Users\Stewart Rogers\AppData\Local\Temp\bl243\.curlrc'",
            79);
        Diagnostics.Act("lines", string.Join(" | ", lines));

        Expect(lines, @"Note: Read config file from 'C:\Users\Stewart ", @"Note: Rogers\AppData\Local\Temp\bl243\.curlrc'");
        CollectionAssert.AreEqual(
            new[]
            {
                @"Note: Read config file from 'C:\Users\Stewart ",
                @"Note: Rogers\AppData\Local\Temp\bl243\.curlrc'",
            },
            lines.ToArray());
    }

    [TestMethod]
    public void WrapText_MeasuredWarningAtColumns200_IsCurlsOneLine()
    {
        IReadOnlyList<string> lines = WrapText(MeasuredText, 200);

        Expect(lines, "Warning: Failed to open the file C:/Windows/System32/bl087.txt: Permission denied");
        CollectionAssert.AreEqual(
            new[] { "Warning: Failed to open the file C:/Windows/System32/bl087.txt: Permission denied" },
            lines.ToArray());
    }

    [TestMethod]
    public void WrapText_MeasuredWarningAtColumns40_IsCurlsThreeLines()
    {
        IReadOnlyList<string> lines = WrapText(MeasuredText, 40);

        Expect(lines, "Warning: Failed to open the file ", "Warning: C:/Windows/System32/bl087.txt: ", "Warning: Permission denied");
        CollectionAssert.AreEqual(
            new[]
            {
                "Warning: Failed to open the file ",
                "Warning: C:/Windows/System32/bl087.txt: ",
                "Warning: Permission denied",
            },
            lines.ToArray());
    }

    [TestMethod]
    public void WrapText_NoBlankBeforeWidthMinusOne_CutsAfterWidthMinusOne()
    {
        IReadOnlyList<string> lines = WrapText("abcdefghijklmnop qr", 19);

        Expect(lines, "Warning: abcdefghij", "Warning: klmnop qr");
        CollectionAssert.AreEqual(new[] { "Warning: abcdefghij", "Warning: klmnop qr" }, lines.ToArray());
    }

    [TestMethod]
    public void WrapText_OnlyBlankAtIndexZero_CutsAfterWidthMinusOne()
    {
        IReadOnlyList<string> lines = WrapText(" bcdefghijklm", 19);

        Expect(lines, "Warning:  bcdefghij", "Warning: klm");
        CollectionAssert.AreEqual(new[] { "Warning:  bcdefghij", "Warning: klm" }, lines.ToArray());
    }

    [TestMethod]
    public void WrapText_TabBeforeWidth_IsACutBlank()
    {
        IReadOnlyList<string> lines = WrapText("abc\tdefghijkl", 19);

        Expect(lines, "Warning: abc\t", "Warning: defghijkl");
        CollectionAssert.AreEqual(new[] { "Warning: abc\t", "Warning: defghijkl" }, lines.ToArray());
    }

    [TestMethod]
    public void WrapText_TextExactlyTheWidth_StaysOneLine()
    {
        IReadOnlyList<string> lines = WrapText("abcdefghij", 19);

        Expect(lines, "Warning: abcdefghij");
        CollectionAssert.AreEqual(new[] { "Warning: abcdefghij" }, lines.ToArray());
    }

    [TestMethod]
    [DataRow(9)]
    [DataRow(1)]
    public void WrapText_ColumnsNoWiderThanThePrefix_LeavesTheTextWhole(int columns)
    {
        IReadOnlyList<string> lines = WrapText(MeasuredText, columns);

        Expect(lines, "Warning: " + MeasuredText);
        CollectionAssert.AreEqual(new[] { "Warning: " + MeasuredText }, lines.ToArray());
    }

    [TestMethod]
    public void WrapLine_WarningLine_WrapsTheTextAfterThePrefix()
    {
        IReadOnlyList<string> lines = WrapLine("Warning: " + MeasuredText, 40);

        Diagnostics.Assert("line count / first line", "3 / Warning: Failed to open the file ", $"{lines.Count} / {lines[0]}");
        Assert.HasCount(3, lines);
        Assert.AreEqual("Warning: Failed to open the file ", lines[0]);
    }

    [TestMethod]
    public void WrapLine_NonWarningLine_IsReturnedUnchanged()
    {
        string line = "curl: (23) client returned ERROR on write of 92 bytes and more text to pass forty columns";

        IReadOnlyList<string> lines = WrapLine(line, 40);

        Expect(lines, line);
        CollectionAssert.AreEqual(new[] { line }, lines.ToArray());
    }

    private IReadOnlyList<string> WrapText(string text, int columns)
    {
        Diagnostics.Arrange("warning text / columns", $"{text} / {columns}");
        IReadOnlyList<string> lines = WarningLineWrapper.WrapText(text, columns);
        Diagnostics.Act("lines", string.Join(" | ", lines));
        return lines;
    }

    private IReadOnlyList<string> WrapLine(string line, int columns)
    {
        Diagnostics.Arrange("line / columns", $"{line} / {columns}");
        IReadOnlyList<string> lines = WarningLineWrapper.WrapLine(line, columns);
        Diagnostics.Act("lines", string.Join(" | ", lines));
        return lines;
    }

    private void Expect(IReadOnlyList<string> lines, params string[] expected) =>
        Diagnostics.Assert("lines", string.Join(" | ", expected), string.Join(" | ", lines));
}
