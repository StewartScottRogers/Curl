namespace Curl.Cli;

/// <summary>
/// Pins the width curl 8.21.0 wraps its error and warning lines to. Measured on Windows on 2026-10-08
/// with <c>curl -K C:/Temp/aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa/bbbbbbbbbbbbbbbbbbbbbbbb/log/missing http://127.0.0.1:1/</c>
/// with no <c>COLUMNS</c>, <c>COLUMNS=200</c> and <c>COLUMNS=40</c>: the first line wraps at 79, 200 and
/// 40 columns, exit 26 each time (upstream test411).
/// </summary>
[TestClass]
public sealed class WrappedMessageTests
{
    private const string Message = "cannot read config from 'C:/Temp/aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa/bbbbbbbbbbbbbbbbbbbbbbbb/log/missing'";

    [TestMethod]
    [DataRow(null, 79)]
    [DataRow("200", 200)]
    [DataRow("40", 40)]
    [DataRow(" +21", 21)]
    [DataRow("9999", 9999)]
    [DataRow("20", 79)]
    [DataRow("10000", 79)]
    [DataRow("80x", 79)]
    [DataRow("", 79)]
    public void TerminalColumns_ColumnsVariable_GivesCurlsWidth(string? columnsVariable, int expected)
    {
        int columns = WrappedMessage.TerminalColumns(columnsVariable);

        Assert.AreEqual(expected, columns);
    }

    [TestMethod]
    public void Lines_AtSeventyNineColumns_WrapsAsCurlDoes()
    {
        IReadOnlyList<string> lines = WrappedMessage.Lines("curl: ", Message, 79);

        CollectionAssert.AreEqual(
            new[]
            {
                "curl: cannot read config from ",
                "curl: 'C:/Temp/aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa/bbbbbbbbbbbbbbbbbbbbbbbb/log/mi",
                "curl: ssing'",
            },
            lines.ToArray());
    }

    [TestMethod]
    public void Lines_AtTwoHundredColumns_KeepsOneLine()
    {
        IReadOnlyList<string> lines = WrappedMessage.Lines("curl: ", Message, 200);

        CollectionAssert.AreEqual(new[] { "curl: " + Message }, lines.ToArray());
    }

    [TestMethod]
    public void Lines_AtFortyColumns_WrapsAsCurlDoes()
    {
        IReadOnlyList<string> lines = WrappedMessage.Lines("curl: ", Message, 40);

        CollectionAssert.AreEqual(
            new[]
            {
                "curl: cannot read config from ",
                "curl: 'C:/Temp/aaaaaaaaaaaaaaaaaaaaaaaaa",
                "curl: aaaaaaa/bbbbbbbbbbbbbbbbbbbbbbbb/l",
                "curl: og/missing'",
            },
            lines.ToArray());
    }
}
