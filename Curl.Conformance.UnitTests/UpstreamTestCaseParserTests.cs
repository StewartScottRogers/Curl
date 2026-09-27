using System.Text;

namespace Curl.Conformance;

/// <summary>
/// Pins <see cref="UpstreamTestCaseParser"/> against inline test files written in the format
/// of curl's <c>docs/tests/FILEFORMAT.md</c> at <c>curl-8_21_0</c>.
/// </summary>
[TestClass]
public sealed class UpstreamTestCaseParserTests
{
    private const string EverySection =
        """
        <testcase>
        <info>
        <keywords>
        HTTP
        HTTP GET
        </keywords>
        </info>

        <reply>
        <data>
        HTTP/1.1 200 OK
        </data>
        <data1>
        second reply
        </data1>
        <servercmd>
        auth_required
        </servercmd>
        </reply>

        <client>
        <server>
        http
        </server>
        <features>
        cookies
        </features>
        <tool>
        lib1500
        </tool>
        <name>
        HTTP GET
        </name>
        <command>
        http://%HOSTIP:%HTTPPORT/%TESTNUMBER
        </command>
        <file name="%LOGDIR/first">
        one
        </file>
        <file name="%LOGDIR/second">
        two
        </file>
        <stdin>
        typed
        </stdin>
        </client>

        <verify>
        <protocol>
        GET / HTTP/1.1
        </protocol>
        <stdout>
        printed
        </stdout>
        <stderr>
        warned
        </stderr>
        <errorcode>
        7
        </errorcode>
        <strip>
        ^User-Agent:.*
        </strip>
        <strippart>
        s/boundary=.*//
        </strippart>
        <file name="%LOGDIR/out" mode="text">
        written
        </file>
        </verify>
        </testcase>

        """;

    [TestMethod]
    [DataRow("info", "keywords", "HTTP\nHTTP GET\n")]
    [DataRow("reply", "data", "HTTP/1.1 200 OK\n")]
    [DataRow("reply", "data1", "second reply\n")]
    [DataRow("reply", "servercmd", "auth_required\n")]
    [DataRow("client", "server", "http\n")]
    [DataRow("client", "features", "cookies\n")]
    [DataRow("client", "tool", "lib1500\n")]
    [DataRow("client", "name", "HTTP GET\n")]
    [DataRow("client", "command", "http://%HOSTIP:%HTTPPORT/%TESTNUMBER\n")]
    [DataRow("client", "stdin", "typed\n")]
    [DataRow("verify", "protocol", "GET / HTTP/1.1\n")]
    [DataRow("verify", "stdout", "printed\n")]
    [DataRow("verify", "stderr", "warned\n")]
    [DataRow("verify", "errorcode", "7\n")]
    [DataRow("verify", "strip", "^User-Agent:.*\n")]
    [DataRow("verify", "strippart", "s/boundary=.*//\n")]
    public void Parse_EverySection_ExposesEachPartAsItsBodyBytes(string section, string name, string expectedBody)
    {
        UpstreamTestCase testCase = ParseSuccessfully(EverySection);

        UpstreamTestSection? part = testCase.Find(section, name);

        Assert.IsNotNull(part);
        Assert.AreEqual(section, part.Section);
        Assert.AreEqual(name, part.Name);
        AssertBytes(expectedBody, part.Content);
    }

    [TestMethod]
    public void Parse_TwoClientFiles_ExposesEachWithItsNameAttribute()
    {
        UpstreamTestCase testCase = ParseSuccessfully(EverySection);

        UpstreamTestSection[] files = [.. testCase.FindAll("client", "file")];

        Assert.HasCount(2, files);
        Assert.AreEqual("%LOGDIR/first", files[0].GetAttribute("name"));
        AssertBytes("one\n", files[0].Content);
        Assert.AreEqual("%LOGDIR/second", files[1].GetAttribute("name"));
        AssertBytes("two\n", files[1].Content);
    }

    [TestMethod]
    public void Parse_VerifyFileInTextMode_KeepsItsAttributesAndBodyUnchanged()
    {
        UpstreamTestCase testCase = ParseSuccessfully(EverySection);

        UpstreamTestSection? file = testCase.Find("verify", "file");

        Assert.IsNotNull(file);
        Assert.AreEqual("%LOGDIR/out", file.GetAttribute("name"));
        Assert.AreEqual("text", file.GetAttribute("mode"));
        Assert.HasCount(2, file.Attributes);
        AssertBytes("written\n", file.Content);
    }

    [TestMethod]
    public void Parse_EverySection_ListsPartsInFileOrderWithTheirOpeningLine()
    {
        UpstreamTestCase testCase = ParseSuccessfully(EverySection);

        Assert.HasCount(19, testCase.Sections);
        Assert.AreEqual("keywords", testCase.Sections[0].Name);
        Assert.AreEqual(3, testCase.Sections[0].LineNumber);
        Assert.AreEqual("file", testCase.Sections[18].Name);
        Assert.AreEqual(67, testCase.Sections[18].LineNumber);
    }

    [TestMethod]
    public void Find_MissingPartOrAttribute_ReturnsNull()
    {
        UpstreamTestCase testCase = ParseSuccessfully(EverySection);

        Assert.IsNull(testCase.Find("verify", "upload"));
        Assert.IsNull(testCase.Find("reply", "data")!.GetAttribute("nonewline"));
    }

    [TestMethod]
    public void Parse_PartWithLineEndingAttributes_KeepsTheBodyAsWritten()
    {
        UpstreamTestSection part = ParseOnePart("<stdout crlf=\"yes\" nonewline=\"yes\" mode=\"text\">\na\r\nb\n</stdout>\n");

        AssertBytes("a\r\nb\n", part.Content);
        Assert.IsTrue(part.IsAttributeSet("crlf"));
        Assert.IsTrue(part.IsAttributeSet("nonewline"));
        Assert.AreEqual("text", part.GetAttribute("mode"));
    }

    [TestMethod]
    public void Parse_EmptyPart_HasAnEmptyBody()
    {
        UpstreamTestSection part = ParseOnePart("<data>\n</data>\n");

        Assert.IsTrue(part.Content.IsEmpty);
    }

    [TestMethod]
    public void Parse_BodyHoldingAngleBracketsAmpersandsAndTagLikeLines_KeepsItByteForByte()
    {
        const string body = "<html>\na < b && c > d &amp;\n</body>\n  </other>\n<data1>\n</datax>\néÿ\r\n";
        byte[] file = Encoding.Latin1.GetBytes("<testcase>\n<reply>\n<data>\n" + body + "</data>\n</reply>\n</testcase>\n");

        UpstreamTestCaseParseResult result = UpstreamTestCaseParser.Parse(file);

        Assert.IsTrue(result.IsParsed, result.Failure?.Message);
        CollectionAssert.AreEqual(Encoding.Latin1.GetBytes(body), result.TestCase.Find("reply", "data")!.Content.ToArray());
    }

    [TestMethod]
    public void Parse_BodyOpeningATagOfThePartsOwnName_NestsUntilItIsClosed()
    {
        UpstreamTestSection part = ParseOnePart("<data>\n<data>\ninner\n</data>\nouter\n</data>\n");

        AssertBytes("<data>\ninner\n</data>\nouter\n", part.Content);
    }

    [TestMethod]
    public void Parse_TextOutsideAPart_IsIgnored()
    {
        UpstreamTestCase testCase = ParseSuccessfully("<!-- comment -->\n<testcase>\nstray text\n<reply>\n  %if feature\n<data>\nx\n</data>\n</reply>\n</testcase>");

        Assert.HasCount(1, testCase.Sections);
        AssertBytes("x\n", testCase.Sections[0].Content);
    }

    [TestMethod]
    public void Parse_EmptyFile_HasNoSections()
    {
        UpstreamTestCase testCase = ParseSuccessfully("");

        Assert.IsEmpty(testCase.Sections);
    }

    [TestMethod]
    public void Parse_PartNotClosedBeforeItsSectionCloses_FailsNamingThePartAndLine()
    {
        UpstreamTestCaseParseResult result = UpstreamTestCaseParser.Parse(Encoding.Latin1.GetBytes("<testcase>\n<reply>\n<data>\nHTTP/1.1 200 OK\n</reply>\n</testcase>\n"));

        Assert.IsFalse(result.IsParsed);
        Assert.AreEqual("<reply><data>", result.Failure.Section);
        Assert.AreEqual(5, result.Failure.LineNumber);
        Assert.AreEqual("<reply><data> opened on line 3 is not closed before </reply> on line 5.", result.Failure.Message);
    }

    [TestMethod]
    public void Parse_PartUnclosedAtEndOfFile_FailsNamingThePartAndLine()
    {
        UpstreamTestCaseParseResult result = UpstreamTestCaseParser.Parse(Encoding.Latin1.GetBytes("<testcase>\n<reply>\n<data>\nHTTP/1.1 200 OK\n"));

        Assert.IsFalse(result.IsParsed);
        Assert.AreEqual("<reply><data>", result.Failure.Section);
        Assert.AreEqual(3, result.Failure.LineNumber);
        Assert.AreEqual("<reply><data> opened on line 3 is never closed.", result.Failure.Message);
    }

    [TestMethod]
    public void Parse_UnclosedRoot_FailsNamingTheRoot()
    {
        UpstreamTestCaseParseResult result = UpstreamTestCaseParser.Parse(Encoding.Latin1.GetBytes("<testcase>\n"));

        Assert.IsFalse(result.IsParsed);
        Assert.AreEqual("<testcase>", result.Failure.Section);
        Assert.AreEqual(1, result.Failure.LineNumber);
    }

    [TestMethod]
    public void Parse_ClosingTagForAnotherSection_Fails()
    {
        UpstreamTestCaseParseResult result = UpstreamTestCaseParser.Parse(Encoding.Latin1.GetBytes("<testcase>\n<reply>\n</verify>\n</testcase>\n"));

        Assert.IsFalse(result.IsParsed);
        Assert.AreEqual("</verify>", result.Failure.Section);
        Assert.AreEqual(3, result.Failure.LineNumber);
        Assert.AreEqual("</verify> on line 3 does not close an open section.", result.Failure.Message);
    }

    [TestMethod]
    public void Parse_ClosingTagWithNothingOpen_Fails()
    {
        UpstreamTestCaseParseResult result = UpstreamTestCaseParser.Parse(Encoding.Latin1.GetBytes("</testcase>\n"));

        Assert.IsFalse(result.IsParsed);
        Assert.AreEqual(1, result.Failure.LineNumber);
    }

    [TestMethod]
    [DataRow("text >")]
    [DataRow("<")]
    [DataRow("<>")]
    [DataRow("<!-- note -->")]
    [DataRow("<info")]
    [DataRow("<info/>")]
    [DataRow("</>")]
    [DataRow("</info")]
    [DataRow("</info/>")]
    [DataRow("\t</reply>")]
    public void Parse_LineThatIsNotATagOutsideAPart_IsIgnored(string line)
    {
        UpstreamTestCaseParseResult result = ParseOnePartFile(line + "\n<data>\nx\n</data>\n");

        Assert.IsTrue(result.IsParsed, result.Failure?.Message);
        Assert.HasCount(1, result.TestCase.Sections);
        Assert.AreEqual("data", result.TestCase.Sections[0].Name);
    }

    [TestMethod]
    public void Parse_TagWithIndentTrailingTextAndMixedQuotes_ReadsNameAndEveryAttribute()
    {
        UpstreamTestSection part = ParseOnePart("  <data_1-x a=\"1\"   b = 'two words' c=\"\">  trailing\r\nbody\n  </data_1-x> trailing\n");

        Assert.AreEqual("data_1-x", part.Name);
        Assert.AreEqual("1", part.GetAttribute("a"));
        Assert.AreEqual("two words", part.GetAttribute("b "));
        Assert.AreEqual(string.Empty, part.GetAttribute("c"));
        AssertBytes("body\n", part.Content);
    }

    [TestMethod]
    public void Parse_TagWithoutClosingBracket_ReadsItsAttributes()
    {
        UpstreamTestSection part = ParseOnePart("<data nonewline=\"yes\"\nbody\n</data>\n");

        Assert.AreEqual("yes", part.GetAttribute("nonewline"));
    }

    [TestMethod]
    [DataRow("<data a=b c=\"d\">")]
    [DataRow("<data a=\"unclosed>")]
    [DataRow("<data a=>")]
    [DataRow("<data a=")]
    public void Parse_AttributeThatIsNotQuoted_StopsReadingAttributes(string openingTag)
    {
        UpstreamTestSection part = ParseOnePart(openingTag + "\nbody\n</data>\n");

        Assert.IsEmpty(part.Attributes);
        AssertBytes("body\n", part.Content);
    }

    private static UpstreamTestCase ParseSuccessfully(string file)
    {
        UpstreamTestCaseParseResult result = UpstreamTestCaseParser.Parse(Encoding.Latin1.GetBytes(file.ReplaceLineEndings("\n")));

        Assert.IsTrue(result.IsParsed, result.Failure?.Message);
        Assert.IsNull(result.Failure);
        return result.TestCase;
    }

    private static UpstreamTestCaseParseResult ParseOnePartFile(string part) =>
        UpstreamTestCaseParser.Parse(Encoding.Latin1.GetBytes("<testcase>\n<reply>\n" + part + "</reply>\n</testcase>\n"));

    private static UpstreamTestSection ParseOnePart(string part)
    {
        UpstreamTestCaseParseResult result = ParseOnePartFile(part);

        Assert.IsTrue(result.IsParsed, result.Failure?.Message);
        Assert.HasCount(1, result.TestCase.Sections);
        return result.TestCase.Sections[0];
    }

    private static void AssertBytes(string expected, ReadOnlyMemory<byte> actual) =>
        CollectionAssert.AreEqual(Encoding.Latin1.GetBytes(expected), actual.ToArray());
}
