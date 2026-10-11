using System.Text;

namespace Curl.Conformance;

/// <summary>
/// Pins <see cref="UpstreamWrappedMessageLines"/> against how curl 8.21.0's <c>voutf</c> wraps a
/// <c>Note: </c> or <c>Warning: </c> message at 79 columns.
/// </summary>
[TestClass]
public sealed class UpstreamWrappedMessageLinesTests
{
    [TestMethod]
    public void Unwrap_NoteCutAfterABlankWithCrlf_JoinsTheLinesAndDropsTheRepeatedPrefix()
    {
        string wrapped = "Note: skips transfer, \r\nNote: \"there\" exists locally\r\n";

        string unwrapped = Unwrap(wrapped);

        Assert.AreEqual("Note: skips transfer, \"there\" exists locally\r\n", unwrapped);
    }

    [TestMethod]
    public void Unwrap_WarningCutAfterATabAndAtTheFullWidth_JoinsAllThreeLines()
    {
        string fullWidth = "Warning: " + new string('w', 70);
        string wrapped = "Warning: a\t\n" + fullWidth + "\nWarning: end\n";

        string unwrapped = Unwrap(wrapped);

        Assert.AreEqual("Warning: a\t" + new string('w', 70) + "end\n", unwrapped);
    }

    [TestMethod]
    public void Unwrap_SeparateMessages_LeavesThemOnTheirOwnLines()
    {
        string text = "Note: one\nNote: two\nWarning: \nNote: three\ncurl: (6) x\nNote: last ";

        string unwrapped = Unwrap(text);

        Assert.AreEqual(text, unwrapped);
    }

    [TestMethod]
    public void Unwrap_Empty_ReturnsNothing()
    {
        string unwrapped = Unwrap("");

        Assert.AreEqual("", unwrapped);
    }

    private static string Unwrap(string text) =>
        Encoding.Latin1.GetString(UpstreamWrappedMessageLines.Unwrap(Encoding.Latin1.GetBytes(text)));
}
