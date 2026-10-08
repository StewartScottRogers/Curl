using Curl.Testing;

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

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public void Resolve_Columns200_Uses200()
    {
        Assert.AreEqual(200, Resolve("200", ConsoleColumns, 200));
    }

    [TestMethod]
    [DataRow("21", 21)]
    [DataRow("9999", 9999)]
    [DataRow(" +80", 80)]
    public void Resolve_ColumnsInRange_UsesIt(string columns, int expected)
    {
        Assert.AreEqual(expected, Resolve(columns, ConsoleColumns, expected));
    }

    [TestMethod]
    [DataRow("20")]
    [DataRow("10000")]
    [DataRow("wide")]
    [DataRow("80x")]
    [DataRow("")]
    public void Resolve_ColumnsOutOfRangeOrNotANumber_UsesTheConsoleWidth(string columns)
    {
        Assert.AreEqual(ConsoleColumns, Resolve(columns, ConsoleColumns, ConsoleColumns));
    }

    [TestMethod]
    public void Resolve_NoColumnsAndNoConsole_Gives79()
    {
        Assert.AreEqual(79, Resolve(null, null, 79));
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(10000)]
    public void Resolve_NoColumnsAndConsoleWidthOutOfRange_Gives79(int consoleColumns)
    {
        Assert.AreEqual(79, Resolve(null, consoleColumns, 79));
    }

    [TestMethod]
    public void Resolve_NoColumnsAndConsoleWidth9999_UsesIt()
    {
        Assert.AreEqual(9999, Resolve(null, 9999, 9999));
    }

    [TestMethod]
    public void ReadStandardErrorConsoleColumns_StandardErrorRedirected_IsNullWithoutReadingTheWidth()
    {
        Diagnostics.Arrange("stderr redirected / runs on Windows", "True / True");
        int? columns = TerminalColumns.ReadStandardErrorConsoleColumns(
            isErrorRedirected: true,
            () => throw new AssertFailedException("The width was read."),
            runsOnWindows: true);
        Diagnostics.Act("columns", columns?.ToString() ?? "null");

        Diagnostics.Assert("columns", "null", columns?.ToString() ?? "null");
        Assert.IsNull(columns);
    }

    [TestMethod]
    public void ReadStandardErrorConsoleColumns_80ColumnConsoleOnWindows_Is79AsCurlsRightMinusLeft()
    {
        Assert.AreEqual(79, ReadConsoleColumns(80, runsOnWindows: true, 79));
    }

    [TestMethod]
    public void ReadStandardErrorConsoleColumns_80ColumnConsoleElsewhere_Is80()
    {
        Assert.AreEqual(80, ReadConsoleColumns(80, runsOnWindows: false, 80));
    }

    [TestMethod]
    public void ReadStandardErrorConsoleColumns_WidthCannotBeRead_IsNull()
    {
        Diagnostics.Arrange("stderr redirected / reading the width", "False / throws IOException");
        int? columns = TerminalColumns.ReadStandardErrorConsoleColumns(
            isErrorRedirected: false,
            () => throw new IOException("The handle is invalid."),
            runsOnWindows: true);
        Diagnostics.Act("columns", columns?.ToString() ?? "null");

        Diagnostics.Assert("columns", "null", columns?.ToString() ?? "null");
        Assert.IsNull(columns);
    }

    [TestMethod]
    public void ReadStandardErrorConsoleColumns_ThisProcess_IsNullOrAWidth()
    {
        Diagnostics.Arrange("standard error", "this test host's");
        int? columns = TerminalColumns.ReadStandardErrorConsoleColumns();
        bool nullOrWidth = columns is null or >= -1;
        Diagnostics.Act("is null or a width", nullOrWidth);

        Diagnostics.Assert("is null or a width", true, nullOrWidth);
        Assert.IsTrue(columns is null or >= -1, $"Unexpected width {columns}.");
    }

    [TestMethod]
    public void ReadConsoleWindowWidth_ThisProcess_ReadsTheWidthOrThrowsIOException()
    {
        Diagnostics.Arrange("console", "this test host's");
        try
        {
            int width = TerminalColumns.ReadConsoleWindowWidth();
            Diagnostics.Act("outcome", "read a width");
            Diagnostics.Assert("width is not negative", true, width >= 0);
            Assert.IsGreaterThanOrEqualTo(0, width);
        }
        catch (IOException)
        {
            // No console is attached to the test host, which is how a redirected run reads.
            Diagnostics.Act("outcome", "IOException");
            Diagnostics.Assert("outcome", "IOException", "IOException");
        }
    }

    [TestMethod]
    public void Resolve_ThisProcess_IsAWidthCurlWouldUse()
    {
        Diagnostics.Arrange("environment and console", "this test host's");
        int columns = TerminalColumns.Resolve();
        bool usable = columns is > 0 and < 10000;
        Diagnostics.Act("is a width curl would use", usable);

        Diagnostics.Assert("is a width curl would use", true, usable);
        Assert.IsTrue(columns is > 0 and < 10000, $"Unexpected width {columns}.");
    }

    private int Resolve(string? columnsVariable, int? consoleColumns, int expected)
    {
        Diagnostics.Arrange("COLUMNS", columnsVariable is null ? "unset" : $"'{columnsVariable}'");
        Diagnostics.Arrange("console columns", consoleColumns?.ToString() ?? "no console");
        int columns = TerminalColumns.Resolve(columnsVariable, () => consoleColumns);
        Diagnostics.Act("columns", columns);
        Diagnostics.Assert("columns", expected, columns);
        return columns;
    }

    private int? ReadConsoleColumns(int width, bool runsOnWindows, int expected)
    {
        Diagnostics.Arrange("stderr redirected / console width / runs on Windows", $"False / {width} / {runsOnWindows}");
        int? columns = TerminalColumns.ReadStandardErrorConsoleColumns(false, () => width, runsOnWindows);
        Diagnostics.Act("columns", columns?.ToString() ?? "null");
        Diagnostics.Assert("columns", expected.ToString(), columns?.ToString() ?? "null");
        return columns;
    }
}
