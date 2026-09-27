namespace Curl.Console;

/// <summary>
/// Pins warning wrapping against curl 8.21.0, measured on Windows on 2026-09-26 with
/// <c>curl --no-progress-meter -o C:/Windows/System32/bl087.txt file:///C:/Windows/win.ini</c>.
/// </summary>
[TestClass]
public sealed class WarningLineWrapperTests
{
    private const string MeasuredText = "Failed to open the file C:/Windows/System32/bl087.txt: Permission denied";

    [TestMethod]
    public void WrapText_MeasuredWarningAtDefault79Columns_IsCurlsTwoLines()
    {
        IReadOnlyList<string> lines = WarningLineWrapper.WrapText(MeasuredText, 79);

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
        IReadOnlyList<string> lines = WarningLineWrapper.WrapNoteText(
            @"Read config file from 'C:\Users\Stewart Rogers\AppData\Local\Temp\bl243\.curlrc'",
            79);

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
        IReadOnlyList<string> lines = WarningLineWrapper.WrapText(MeasuredText, 200);

        CollectionAssert.AreEqual(
            new[] { "Warning: Failed to open the file C:/Windows/System32/bl087.txt: Permission denied" },
            lines.ToArray());
    }

    [TestMethod]
    public void WrapText_MeasuredWarningAtColumns40_IsCurlsThreeLines()
    {
        IReadOnlyList<string> lines = WarningLineWrapper.WrapText(MeasuredText, 40);

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
        IReadOnlyList<string> lines = WarningLineWrapper.WrapText("abcdefghijklmnop qr", 19);

        CollectionAssert.AreEqual(new[] { "Warning: abcdefghij", "Warning: klmnop qr" }, lines.ToArray());
    }

    [TestMethod]
    public void WrapText_OnlyBlankAtIndexZero_CutsAfterWidthMinusOne()
    {
        IReadOnlyList<string> lines = WarningLineWrapper.WrapText(" bcdefghijklm", 19);

        CollectionAssert.AreEqual(new[] { "Warning:  bcdefghij", "Warning: klm" }, lines.ToArray());
    }

    [TestMethod]
    public void WrapText_TabBeforeWidth_IsACutBlank()
    {
        IReadOnlyList<string> lines = WarningLineWrapper.WrapText("abc\tdefghijkl", 19);

        CollectionAssert.AreEqual(new[] { "Warning: abc\t", "Warning: defghijkl" }, lines.ToArray());
    }

    [TestMethod]
    public void WrapText_TextExactlyTheWidth_StaysOneLine()
    {
        IReadOnlyList<string> lines = WarningLineWrapper.WrapText("abcdefghij", 19);

        CollectionAssert.AreEqual(new[] { "Warning: abcdefghij" }, lines.ToArray());
    }

    [TestMethod]
    [DataRow(9)]
    [DataRow(1)]
    public void WrapText_ColumnsNoWiderThanThePrefix_LeavesTheTextWhole(int columns)
    {
        IReadOnlyList<string> lines = WarningLineWrapper.WrapText(MeasuredText, columns);

        CollectionAssert.AreEqual(new[] { "Warning: " + MeasuredText }, lines.ToArray());
    }

    [TestMethod]
    public void WrapLine_WarningLine_WrapsTheTextAfterThePrefix()
    {
        IReadOnlyList<string> lines = WarningLineWrapper.WrapLine("Warning: " + MeasuredText, 40);

        Assert.HasCount(3, lines);
        Assert.AreEqual("Warning: Failed to open the file ", lines[0]);
    }

    [TestMethod]
    public void WrapLine_NonWarningLine_IsReturnedUnchanged()
    {
        string line = "curl: (23) client returned ERROR on write of 92 bytes and more text to pass forty columns";

        IReadOnlyList<string> lines = WarningLineWrapper.WrapLine(line, 40);

        CollectionAssert.AreEqual(new[] { line }, lines.ToArray());
    }
}
