namespace Curl.Console;

/// <summary>
/// Pins the terminal-width resolution against curl 8.21.0's <c>get_terminal_columns</c>:
/// <c>COLUMNS</c> from 21 to 9999, then the standard-error console, then 79. Every test
/// injects the environment value and the console width.
/// </summary>
[TestClass]
public sealed class TerminalColumnsTests
{
    private const int ConsoleColumns = 119;

    [TestMethod]
    public void Resolve_Columns200_Uses200()
    {
        Assert.AreEqual(200, TerminalColumns.Resolve("200", () => ConsoleColumns));
    }

    [TestMethod]
    [DataRow("21", 21)]
    [DataRow("9999", 9999)]
    [DataRow(" +80", 80)]
    public void Resolve_ColumnsInRange_UsesIt(string columns, int expected)
    {
        Assert.AreEqual(expected, TerminalColumns.Resolve(columns, () => ConsoleColumns));
    }

    [TestMethod]
    [DataRow("20")]
    [DataRow("10000")]
    [DataRow("wide")]
    [DataRow("80x")]
    [DataRow("")]
    public void Resolve_ColumnsOutOfRangeOrNotANumber_UsesTheConsoleWidth(string columns)
    {
        Assert.AreEqual(ConsoleColumns, TerminalColumns.Resolve(columns, () => ConsoleColumns));
    }

    [TestMethod]
    public void Resolve_NoColumnsAndNoConsole_Gives79()
    {
        Assert.AreEqual(79, TerminalColumns.Resolve(null, () => null));
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(10000)]
    public void Resolve_NoColumnsAndConsoleWidthOutOfRange_Gives79(int consoleColumns)
    {
        Assert.AreEqual(79, TerminalColumns.Resolve(null, () => consoleColumns));
    }

    [TestMethod]
    public void Resolve_NoColumnsAndConsoleWidth9999_UsesIt()
    {
        Assert.AreEqual(9999, TerminalColumns.Resolve(null, () => 9999));
    }

    [TestMethod]
    public void ReadStandardErrorConsoleColumns_StandardErrorRedirected_IsNullWithoutReadingTheWidth()
    {
        int? columns = TerminalColumns.ReadStandardErrorConsoleColumns(
            isErrorRedirected: true,
            () => throw new AssertFailedException("The width was read."),
            runsOnWindows: true);

        Assert.IsNull(columns);
    }

    [TestMethod]
    public void ReadStandardErrorConsoleColumns_80ColumnConsoleOnWindows_Is79AsCurlsRightMinusLeft()
    {
        Assert.AreEqual(79, TerminalColumns.ReadStandardErrorConsoleColumns(false, () => 80, runsOnWindows: true));
    }

    [TestMethod]
    public void ReadStandardErrorConsoleColumns_80ColumnConsoleElsewhere_Is80()
    {
        Assert.AreEqual(80, TerminalColumns.ReadStandardErrorConsoleColumns(false, () => 80, runsOnWindows: false));
    }

    [TestMethod]
    public void ReadStandardErrorConsoleColumns_WidthCannotBeRead_IsNull()
    {
        int? columns = TerminalColumns.ReadStandardErrorConsoleColumns(
            isErrorRedirected: false,
            () => throw new IOException("The handle is invalid."),
            runsOnWindows: true);

        Assert.IsNull(columns);
    }

    [TestMethod]
    public void ReadStandardErrorConsoleColumns_ThisProcess_IsNullOrAWidth()
    {
        int? columns = TerminalColumns.ReadStandardErrorConsoleColumns();

        Assert.IsTrue(columns is null or >= -1, $"Unexpected width {columns}.");
    }

    [TestMethod]
    public void ReadConsoleWindowWidth_ThisProcess_ReadsTheWidthOrThrowsIOException()
    {
        try
        {
            Assert.IsGreaterThanOrEqualTo(0, TerminalColumns.ReadConsoleWindowWidth());
        }
        catch (IOException)
        {
            // No console is attached to the test host, which is how a redirected run reads.
        }
    }

    [TestMethod]
    public void Resolve_ThisProcess_IsAWidthCurlWouldUse()
    {
        int columns = TerminalColumns.Resolve();

        Assert.IsTrue(columns is > 0 and < 10000, $"Unexpected width {columns}.");
    }
}
