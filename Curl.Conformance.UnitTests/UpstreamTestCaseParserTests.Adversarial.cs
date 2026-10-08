using System.Text;
using Curl.Testing;

namespace Curl.Conformance;

// Attacks the parser through its public surface with empty, truncated, deeply nested, malformed
// and random files, by Documentation/Wiki/Adversarial-Testing.md (BL-1494): every answer is a
// parsed case or a named failure, never an exception.
public sealed partial class UpstreamTestCaseParserTests
{
    private const string AdversarialValidFile =
        "<testcase>\n<reply>\n<data crlf=\"yes\">\nHTTP/1.1 200 OK\n</data>\n</reply>\n<verify>\n<protocol>\nGET / HTTP/1.1\n</protocol>\n</verify>\n</testcase>\n";

    [TestMethod]
    public void Parse_EmptyFile_ParsesAsACaseWithNoSections()
    {
        UpstreamTestCaseParseResult result = UpstreamTestCaseParser.Parse([]);

        Diagnostics.Assert("parsed", true, result.IsParsed);
        Assert.IsTrue(result.IsParsed);
        Assert.IsEmpty(result.TestCase.Sections);
    }

    [TestMethod]
    public void Parse_LastLineWithoutLineFeed_StillClosesTheFile()
    {
        UpstreamTestCaseParseResult result = ParseLatin1("<testcase>\n<reply>\n<data>\nx\n</data>\n</reply>\n</testcase>");

        Diagnostics.Assert("parsed", true, result.IsParsed);
        Assert.IsTrue(result.IsParsed);
        Assert.AreEqual("x\n", BodyText(result.TestCase.Find("reply", "data")!.Content));
    }

    [TestMethod]
    public void Parse_TruncatedAtEveryOffset_ReturnsAResultWithoutThrowing()
    {
        byte[] file = Encoding.Latin1.GetBytes(AdversarialValidFile);
        Diagnostics.Arrange("file", AdversarialValidFile);

        for (int length = 0; length <= file.Length; length++)
        {
            UpstreamTestCaseParseResult result = UpstreamTestCaseParser.Parse(file.AsSpan(0, length));

            bool parsedOrNamed = result.IsParsed || (result.Failure.LineNumber >= 1 && result.Failure.Message.Length > 0);
            Assert.IsTrue(parsedOrNamed, $"truncated to {length} bytes");
        }
    }

    [TestMethod]
    public void Parse_TruncatedInsideAPart_NamesThePartThatIsNeverClosed()
    {
        int cut = AdversarialValidFile.IndexOf("</data>", StringComparison.Ordinal);
        UpstreamTestCaseParseResult result = ParseLatin1(AdversarialValidFile[..cut]);

        Diagnostics.Assert("failure", "<reply><data> on line 3", result.Failure is null ? "parsed" : $"{result.Failure.Section} on line {result.Failure.LineNumber}");
        Assert.IsFalse(result.IsParsed);
        Assert.AreEqual("<reply><data>", result.Failure.Section);
        Assert.AreEqual(3, result.Failure.LineNumber);
    }

    [TestMethod]
    public void Parse_TenThousandNestedPartTagsInsideAPart_KeepsThemAsBody()
    {
        const int Depth = 10_000;
        string nested = string.Concat(Enumerable.Repeat("<data>\n", Depth)) + string.Concat(Enumerable.Repeat("</data>\n", Depth));
        UpstreamTestCaseParseResult result = ParseLatin1($"<testcase>\n<reply>\n<data>\n{nested}</data>\n</reply>\n</testcase>\n");

        Assert.IsTrue(result.IsParsed);
        Diagnostics.Assert("body length", nested.Length, result.TestCase.Find("reply", "data")!.Content.Length);
        Assert.AreEqual(nested, BodyText(result.TestCase.Find("reply", "data")!.Content));
    }

    [TestMethod]
    public void Parse_NestedPartTagNeverClosed_FailsAtTheEnclosingSectionsClose()
    {
        UpstreamTestCaseParseResult result = ParseLatin1("<testcase>\n<reply>\n<data>\n<data>\n</data>\n</reply>\n</testcase>\n");

        Assert.IsFalse(result.IsParsed);
        Diagnostics.Assert("failure line", 6, result.Failure.LineNumber);
        Assert.AreEqual(6, result.Failure.LineNumber);
        Assert.AreEqual("<reply><data>", result.Failure.Section);
    }

    [TestMethod]
    public void Parse_ClosingTagOnTheFirstLine_FailsNamingIt()
    {
        UpstreamTestCaseParseResult result = ParseLatin1("</reply>\n");

        Assert.IsFalse(result.IsParsed);
        Diagnostics.Assert("failure", "</reply> on line 1", $"{result.Failure.Section} on line {result.Failure.LineNumber}");
        Assert.AreEqual("</reply>", result.Failure.Section);
        Assert.AreEqual(1, result.Failure.LineNumber);
    }

    [TestMethod]
    public void Parse_SectionClosedByTheWrongName_FailsNamingTheClosingTag()
    {
        UpstreamTestCaseParseResult result = ParseLatin1("<testcase>\n<reply>\n</verify>\n</testcase>\n");

        Assert.IsFalse(result.IsParsed);
        Diagnostics.Assert("failure", "</verify> on line 3", $"{result.Failure.Section} on line {result.Failure.LineNumber}");
        Assert.AreEqual("</verify>", result.Failure.Section);
        Assert.AreEqual(3, result.Failure.LineNumber);
    }

    [TestMethod]
    public void Parse_CarriageReturnOnlyLineEndings_ReadsOneLineThatIsNeverClosed()
    {
        UpstreamTestCaseParseResult result = ParseLatin1(AdversarialValidFile.Replace('\n', '\r'));

        Assert.IsFalse(result.IsParsed);
        Diagnostics.Assert("failure line", 1, result.Failure.LineNumber);
        Assert.AreEqual(1, result.Failure.LineNumber);
    }

    [TestMethod]
    public void Parse_AttributeWithUnterminatedQuote_ReadsNoAttribute()
    {
        UpstreamTestCaseParseResult result = ParseLatin1("<testcase>\n<reply>\n<data crlf=\"yes>\nx\n</data>\n</reply>\n</testcase>\n");

        Assert.IsTrue(result.IsParsed);
        Diagnostics.Assert("attribute count", 0, result.TestCase.Find("reply", "data")!.Attributes.Count);
        Assert.IsEmpty(result.TestCase.Find("reply", "data")!.Attributes);
    }

    [TestMethod]
    public void Parse_DuplicatedAttribute_KeepsTheLastValueWithoutThrowing()
    {
        UpstreamTestCaseParseResult result = ParseLatin1("<testcase>\n<reply>\n<data crlf=\"no\" crlf='yes'>\nx\n</data>\n</reply>\n</testcase>\n");

        Assert.IsTrue(result.IsParsed);
        Diagnostics.Assert("crlf", "yes", result.TestCase.Find("reply", "data")!.GetAttribute("crlf"));
        Assert.AreEqual("yes", result.TestCase.Find("reply", "data")!.GetAttribute("crlf"));
    }

    [TestMethod]
    public void Parse_BodyWithNulAndHighBytes_KeepsEveryByte()
    {
        byte[] body = [0x00, 0xFF, 0x80, (byte)'<', (byte)'&', 0x0D, 0x0A];
        byte[] file = [.. "<testcase>\n<reply>\n<data>\n"u8, .. body, .. "</data>\n</reply>\n</testcase>\n"u8];

        UpstreamTestCaseParseResult result = UpstreamTestCaseParser.Parse(file);

        Assert.IsTrue(result.IsParsed);
        Diagnostics.Assert("body", Convert.ToHexString(body), Convert.ToHexString(result.TestCase.Find("reply", "data")!.Content.Span));
        CollectionAssert.AreEqual(body, result.TestCase.Find("reply", "data")!.Content.ToArray());
    }

    [TestMethod]
    public void Parse_SeededRandomTagLines_NeverThrows()
    {
        const int Seed = 1494;
        string[] lines =
        [
            "<testcase>", "</testcase>", "<reply>", "</reply>", "<data>", "</data>", "<data nocheck=\"yes\">",
            "<verify>", "</verify>", "<", "</", "<>", "</ >", " <data >", "text", "<data crlf='", string.Empty, "\r",
        ];
        Random random = new(Seed);
        Diagnostics.Arrange("seed", Seed);

        for (int file = 0; file < 500; file++)
        {
            string text = string.Concat(Enumerable.Range(0, random.Next(0, 40)).Select(_ => lines[random.Next(lines.Length)] + "\n"));

            UpstreamTestCaseParseResult result = ParseLatin1(text);

            Assert.IsTrue(result.IsParsed || result.Failure.LineNumber >= 1, $"seed {Seed}, file {file}: {text}");
        }
    }

    [TestMethod]
    public async Task Parse_SameFileOnManyTasksAtOnce_GivesTheSameCaseEachTime()
    {
        byte[] file = Encoding.Latin1.GetBytes(AdversarialValidFile);

        string[] answers = await Task.WhenAll(Enumerable.Range(0, 32).Select(_ => Task.Run(() => Describe(UpstreamTestCaseParser.Parse(file)))));

        Diagnostics.Assert("distinct answers", 1, answers.Distinct().Count());
        Assert.HasCount(1, answers.Distinct());
        Assert.AreEqual(Describe(UpstreamTestCaseParser.Parse(file)), answers[0]);
    }

    private static UpstreamTestCaseParseResult ParseLatin1(string file) => UpstreamTestCaseParser.Parse(Encoding.Latin1.GetBytes(file));

    private static string BodyText(ReadOnlyMemory<byte> bytes) => Encoding.Latin1.GetString(bytes.Span);

    private static string Describe(UpstreamTestCaseParseResult result) =>
        result.IsParsed
            ? string.Join("|", result.TestCase.Sections.Select(section => $"{section.Section}/{section.Name}@{section.LineNumber}:{BodyText(section.Content)}"))
            : result.Failure.Message;
}
