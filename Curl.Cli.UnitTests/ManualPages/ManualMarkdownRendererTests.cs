using System.Text;
using Curl.Cli.ManualPages;

namespace Curl.Cli.UnitTests.ManualPages;

[TestClass]
public sealed class ManualMarkdownRendererTests
{
    [TestMethod]
    public void Render_NroffInstruction_ReturnsCode4()
    {
        ManualPageContext context = NewContext();

        List<string> description = Render(context, true, 5, ".SH NAME\n");

        CollectionAssert.AreEqual(new[] { "4" }, description);
        Assert.AreEqual("f.md:6:1:ERROR: nroff instruction in input: \".SH\"\n", context.Error.ToString());
    }

    [TestMethod]
    public void Render_NroffBlock_ReturnsCode3()
    {
        ManualPageContext context = NewContext();

        List<string> description = Render(context, true, 5, ".RS\n");

        CollectionAssert.AreEqual(new[] { "3" }, description);
        Assert.AreEqual("f.md:6:1:ERROR: RS detected, use ##-style\n", context.Error.ToString());
    }

    [TestMethod]
    public void Render_TrailingBlankLine_ExitsWithCode3()
    {
        ManualPageContext context = NewContext();

        ManualPageExit exit = Assert.ThrowsExactly<ManualPageExit>(() => Render(context, true, 5, "Text\n", "\n"));

        Assert.AreEqual(3, exit.ExitCode);
        Assert.AreEqual("f.md:7:1:ERROR: trailing blank line\n", context.Error.ToString());
    }

    [TestMethod]
    public void Render_CommentsAndLeadingBlankLines_AreSkipped()
    {
        ManualPageContext context = NewContext();

        List<string> description = Render(context, true, 5, "\n", "<!-- comment -->\n", "Text\n");

        CollectionAssert.AreEqual(new[] { "Text\n" }, description);
    }

    [TestMethod]
    public void Render_BlankLineBetweenParagraphs_IsKeptAsOne()
    {
        ManualPageContext context = NewContext();

        List<string> description = Render(context, true, 5, "One\n", "\n", "\n", "Two\n");

        CollectionAssert.AreEqual(new[] { "One\n", "\n", "Two\n" }, description);
    }

    [TestMethod]
    public void Render_IndentedLinesOnAManpage_AreWrappedInNfAndFi()
    {
        ManualPageContext context = NewContext();

        List<string> description = Render(context, true, 5, "Text\n", "\n", "    run  -x\n", "Back\n");

        CollectionAssert.AreEqual(new[] { "Text\n", "\n", ".nf\n", "run  \\-x\n", ".fi\n", "Back\n" }, description);
        Assert.AreEqual("", context.Error.ToString());
    }

    [TestMethod]
    public void Render_IndentedLineAtTheEndOfAManpage_IsClosedWithFi()
    {
        ManualPageContext context = NewContext();

        List<string> description = Render(context, true, 5, "    code\n");

        CollectionAssert.AreEqual(new[] { ".nf\n", "code\n", ".fi\n" }, description);
    }

    [TestMethod]
    public void Render_ConsecutiveIndentedLinesOnAManpage_ShareOneNfBlock()
    {
        ManualPageContext context = NewContext();

        List<string> description = Render(context, true, 5, "    a\n", "    b\n");

        CollectionAssert.AreEqual(new[] { ".nf\n", "a\n", "b\n", ".fi\n" }, description);
    }

    [TestMethod]
    public void Render_IndentedLinesInAscii_AreQuoted()
    {
        ManualPageContext context = NewContext();

        List<string> description = Render(context, false, 5, "    code\n", "Back\n");

        CollectionAssert.AreEqual(new[] { "[1q]code\n", "[1]Back\n" }, description);
    }

    [TestMethod]
    [DataRow("```\n")]
    [DataRow("~~~\n")]
    public void Render_FencedBlockOnAManpage_IsWrappedInNfAndFiAndNotChecked(string fence)
    {
        ManualPageContext context = NewContext();

        List<string> description = Render(context, true, 5, fence, "x <y>\n", fence, "After\n");

        CollectionAssert.AreEqual(new[] { ".nf\n", "x <y>\n", ".fi\n", "After\n" }, description);
        Assert.AreEqual("", context.Error.ToString());
    }

    [TestMethod]
    public void Render_FencedBlockInAscii_IsQuoted()
    {
        ManualPageContext context = NewContext();

        List<string> description = Render(context, false, 5, "```\n", "x\n", "```\n");

        CollectionAssert.AreEqual(new[] { "[1q]x\n" }, description);
    }

    [TestMethod]
    [DataRow(true, ".SH NAME\n")]
    [DataRow(false, "[0]NAME\n")]
    public void Render_TitleOnATopLevelPage_IsASection(bool manpage, string expected)
    {
        ManualPageContext context = NewContext();

        List<string> description = Render(context, manpage, 1, "# NAME\n");

        CollectionAssert.AreEqual(new[] { expected }, description);
    }

    [TestMethod]
    public void Render_TitleInAnOption_IsLeftOut()
    {
        ManualPageContext context = NewContext();

        List<string> description = Render(context, true, 5, "# Ignored\n", "Text\n");

        CollectionAssert.AreEqual(new[] { "Text\n" }, description);
    }

    [TestMethod]
    public void Render_ThirdLevelHeader_ExitsWithCode3()
    {
        ManualPageContext context = NewContext();

        ManualPageExit exit = Assert.ThrowsExactly<ManualPageExit>(() => Render(context, true, 5, "### Deep\n"));

        Assert.AreEqual(3, exit.ExitCode);
        Assert.AreEqual("f.md:6:1:ERROR: ### header is not supported\n", context.Error.ToString());
    }

    [TestMethod]
    public void Render_ItemsInAnOptionOnAManpage_AreAnIndentedTable()
    {
        ManualPageContext context = NewContext();

        List<string> description = Render(context, true, 5, "## \"-x, --flag\"\n", "Text\n", "## Second\n", "More\n", "##\n", "After\n");

        CollectionAssert.AreEqual(new[] { ".RS\n", ".IP \"-x, --flag\"\n", "Text\n", ".IP Second\n", "More\n", ".RE\n.IP\n", "After\n" }, description);
    }

    [TestMethod]
    public void Render_ItemsInAnOptionInAscii_AreOneLevelDeeper()
    {
        ManualPageContext context = NewContext();

        List<string> description = Render(context, false, 5, "## \"-x, --flag\"\n", "Text\n", "##\n", "After\n");

        CollectionAssert.AreEqual(new[] { "\n", "[1]-x, --flag\n", "[2]Text\n", "\n", "[1]After\n" }, description);
    }

    [TestMethod]
    public void Render_TableOpenAtTheEndOfAManpage_IsClosed()
    {
        ManualPageContext context = NewContext();

        List<string> description = Render(context, true, 5, "## `item`\n", "Text\n");

        CollectionAssert.AreEqual(new[] { ".RS\n", ".IP item\n", "Text\n", ".RE\n.IP\n" }, description);
    }

    [TestMethod]
    public void Render_EmptyHeaderWithNoTableOpen_AddsNothing()
    {
        ManualPageContext context = NewContext();

        List<string> description = Render(context, true, 5, "##\n", "Text\n");

        CollectionAssert.AreEqual(new[] { "Text\n" }, description);
    }

    [TestMethod]
    [DataRow(true, ".IP item\n")]
    [DataRow(false, "[1]item\n")]
    public void Render_ItemOnATopLevelPage_OpensNoTable(bool manpage, string expected)
    {
        ManualPageContext context = NewContext();

        List<string> description = Render(context, manpage, 1, "## item\n");

        Assert.AreEqual(expected, description[^1]);
        Assert.HasCount(manpage ? 1 : 2, description);
    }

    [TestMethod]
    public void Render_EmptyHeaderOnATopLevelPage_ExitsWithCode3()
    {
        ManualPageContext context = NewContext();

        ManualPageExit exit = Assert.ThrowsExactly<ManualPageExit>(() => Render(context, true, 1, "##\n"));

        Assert.AreEqual(3, exit.ExitCode);
        Assert.AreEqual("f.md:2:1:ERROR: ## empty header top-level mode\n", context.Error.ToString());
    }

    [TestMethod]
    [DataRow("## <file>\n", "f.md:6:1:WARN: un-escaped < or > used: <file>\n")]
    [DataRow("## `<file>`\n", "")]
    public void Render_UnescapedAngleBracketInAnItem_WarnsOutsideCodeSpans(string item, string expected)
    {
        ManualPageContext context = NewContext();

        List<string> description = Render(context, true, 5, item);

        Assert.AreEqual(".IP <file>\n", description[1]);
        Assert.AreEqual(expected, context.Error.ToString());
    }

    [TestMethod]
    public void Render_Placeholders_AreReplacedWithTheRunsValues()
    {
        ManualPageContext context = NewContext();
        context.Version = "8.22.0";
        context.Globals = "G";

        List<string> description = Render(context, false, 5, "Date `%DATE` version `%VERSION` `%GLOBALS`\n");

        CollectionAssert.AreEqual(new[] { "[1]Date 21 Oct 2026 version 8.22.0 G\n" }, description);
    }

    [TestMethod]
    public void Render_TwoSpaces_ReturnsCode3()
    {
        ManualPageContext context = NewContext();

        List<string> description = Render(context, true, 5, "one  two\n");

        CollectionAssert.AreEqual(new[] { "3" }, description);
        Assert.AreEqual("f.md:6:3:ERROR: 2 spaces detected\n", context.Error.ToString());
    }

    [TestMethod]
    public void Render_UnescapedAngleBracketInText_ReturnsCode3()
    {
        ManualPageContext context = NewContext();

        List<string> description = Render(context, true, 5, "a <b> c\n");

        CollectionAssert.AreEqual(new[] { "3" }, description);
        Assert.AreEqual("f.md:6:1:WARN: un-escaped < or > used: a <b> c\n\n", context.Error.ToString());
    }

    [TestMethod]
    public void Render_EscapedAngleBracketsAndCodeSpans_AreWrittenPlain()
    {
        ManualPageContext context = NewContext();

        List<string> description = Render(context, false, 5, "a \\<b\\> `<c>`\n");

        CollectionAssert.AreEqual(new[] { "[1]a <b> \"<c>\"\n" }, description);
        Assert.AreEqual("", context.Error.ToString());
    }

    [TestMethod]
    public void Render_BrokenUpAddedInLine_ReturnsCode3()
    {
        ManualPageContext context = NewContext();

        List<string> description = Render(context, true, 5, "Since (Added in\n");

        CollectionAssert.AreEqual(new[] { "3" }, description);
        Assert.AreEqual("f.md:6:1:ERROR: broken up added-in line:\nf.md:6:1:ERROR: Since (Added in\n", context.Error.ToString());
    }

    [TestMethod]
    [DataRow("Works (Added in 7.10.0)\n", "[1]Works\n")]
    [DataRow("Works (Added in 8.1.0)\n", "[1]Works (Added in 8.1.0)\n")]
    public void Render_AddedInLine_IsRemovedOnlyWhenOlderThan7660(string line, string expected)
    {
        ManualPageContext context = NewContext();

        List<string> description = Render(context, false, 5, line);

        CollectionAssert.AreEqual(new[] { expected }, description);
    }

    [TestMethod]
    public void Render_TextOnAManpage_IsEscapedForNroffWithOptionsAndEmphasisMarkedUp()
    {
        ManualPageContext context = NewContext();

        List<string> description = Render(context, true, 5, "Use --max-time, it's **bold** and *it* a\\b x-y\n");

        CollectionAssert.AreEqual(new[] { "Use \\fI\\-\\-max\\-time\\fP, it\\(aqs \\fBbold\\fP and \\fIit\\fP a\\\\b x\\-y\n" }, description);
    }

    [TestMethod]
    [DataRow(".dot\n", "\\&.dot\n")]
    [DataRow("\"q\" x\n", "\\&\"q\" x\n")]
    public void Render_LineStartingWithADotOrQuoteOnAManpage_IsGuarded(string line, string expected)
    {
        ManualPageContext context = NewContext();

        List<string> description = Render(context, true, 5, line);

        CollectionAssert.AreEqual(new[] { expected }, description);
    }

    [TestMethod]
    public void Render_TextInAscii_DropsTheEmphasisMarks()
    {
        ManualPageContext context = NewContext();

        List<string> description = Render(context, false, 5, "**bold** and *it* --max-time\n");

        CollectionAssert.AreEqual(new[] { "[1]bold and it --max-time\n" }, description);
    }

    private static List<string> Render(ManualPageContext context, bool manpage, int line, params string[] lines) =>
        new ManualMarkdownRenderer(context, manpage, "f.md", line).Render(lines);

    private static ManualPageContext NewContext() => new(_ => null, "21 Oct 2026", new StringBuilder(), new StringBuilder());
}
