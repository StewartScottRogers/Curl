using System.Text;
using Curl.Cli.ManualPages;

namespace Curl.Cli.UnitTests.ManualPages;

[TestClass]
public sealed class ManualAsciiLayoutTests
{
    [TestMethod]
    public void PrintDescription_Manpage_WritesTheLinesAsTheyAre()
    {
        ManualPageContext context = NewContext();

        new ManualAsciiLayout(context).PrintDescription(true, 3, ["[1]a\n", "b\n"]);

        Assert.AreEqual("[1]a\nb\n", context.Output.ToString());
    }

    [TestMethod]
    public void PrintDescription_Paragraph_IsIndentedFourColumnsALevelAndEndsWithABlankLine()
    {
        ManualPageContext context = NewContext();

        new ManualAsciiLayout(context).PrintDescription(false, 0, ["[1]Hello\n", "[1]world\n"]);

        Assert.AreEqual("    Hello world\n\n", context.Output.ToString());
    }

    [TestMethod]
    public void PrintDescription_EightColumnsOfIndent_AreWrittenAsATab()
    {
        ManualPageContext context = NewContext();

        new ManualAsciiLayout(context).PrintDescription(false, 1, ["[1]x\n"]);

        Assert.AreEqual("\tx\n\n", context.Output.ToString());
    }

    [TestMethod]
    public void PrintDescription_LineWithoutALevel_IsAtLevelZero()
    {
        ManualPageContext context = NewContext();

        new ManualAsciiLayout(context).PrintDescription(false, 0, ["plain\n"]);

        Assert.AreEqual("plain\n\n", context.Output.ToString());
    }

    [TestMethod]
    public void PrintDescription_LongParagraph_IsJustifiedToTheColumnWidth()
    {
        ManualPageContext context = NewContext();
        context.ColumnWidth = 20;

        new ManualAsciiLayout(context).PrintDescription(false, 0, ["[0]aaaa bbbb cccc dddd eeee\n"]);

        Assert.AreEqual("aaaa bbbb cccc  dddd\neeee\n\n", context.Output.ToString());
    }

    [TestMethod]
    public void PrintDescription_WordWiderThanTheColumn_GetsALineOfItsOwnAfterAnEmptyOne()
    {
        ManualPageContext context = NewContext();
        context.ColumnWidth = 10;

        new ManualAsciiLayout(context).PrintDescription(false, 0, ["[0]abcdefghijkl xy\n"]);

        Assert.AreEqual("\nabcdefghijkl\nxy\n\n", context.Output.ToString());
    }

    [TestMethod]
    [DataRow("\n", "    One\n\n    Two\n\n")]
    [DataRow("\r\n", "    One\n\n\r\n\n    Two\n\n")]
    public void PrintDescription_LineStartingWithALineBreak_EndsTheParagraph(string separator, string expected)
    {
        ManualPageContext context = NewContext();

        new ManualAsciiLayout(context).PrintDescription(false, 0, ["[1]One\n", separator, "[1]Two\n"]);

        Assert.AreEqual(expected, context.Output.ToString());
    }

    [TestMethod]
    public void PrintDescription_QuotedLine_IsWrittenOneLevelDeeperAndFollowedByABlankLine()
    {
        ManualPageContext context = NewContext();

        new ManualAsciiLayout(context).PrintDescription(false, 0, ["[1]Intro\n", "[1q]code line\n", "[1]After\n"]);

        Assert.AreEqual("    Intro\n\n\tcode line\n\n    After\n\n", context.Output.ToString());
        Assert.AreEqual(0, context.Errors);
    }

    [TestMethod]
    public void PrintDescription_ConsecutiveQuotedLines_HaveNoBlankLineBetweenThem()
    {
        ManualPageContext context = NewContext();

        new ManualAsciiLayout(context).PrintDescription(false, 0, ["[1q]a\n", "[1q]  b\n"]);

        Assert.AreEqual("\ta\n\t  b\n", context.Output.ToString());
    }

    [TestMethod]
    public void PrintDescription_QuotedLineWiderThanTheColumn_CountsAnError()
    {
        ManualPageContext context = NewContext();
        context.ColumnWidth = 20;

        new ManualAsciiLayout(context).PrintDescription(false, 0, ["[1q]0123456789abcdef\n"]);

        Assert.AreEqual("\t0123456789abcdef\n", context.Output.ToString());
        Assert.AreEqual("ERROR: 24 columns is too long\n0123456789abcdef\n", context.Error.ToString());
        Assert.AreEqual(1, context.Errors);
    }

    [TestMethod]
    public void LastLine_LeadingSpaces_AreAddedToTheIndent()
    {
        ManualPageContext context = NewContext();

        new ManualAsciiLayout(context).LastLine(2, ["  a", "b"]);

        Assert.AreEqual("\t  a b\n", context.Output.ToString());
    }

    private static ManualPageContext NewContext() => new(_ => null, "21 Oct 2026", new StringBuilder(), new StringBuilder());
}
