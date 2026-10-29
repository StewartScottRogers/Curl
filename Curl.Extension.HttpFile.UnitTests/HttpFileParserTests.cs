using Curl.Extension.Abstractions.Arguments;

namespace Curl.Extension.HttpFile;

/// <summary>Checks <see cref="HttpFileParser" /> against the request syntax of the Visual Studio .http file format.</summary>
[TestClass]
public sealed class HttpFileParserTests
{
    [TestMethod]
    public void Parse_CommentsBeforeTheRequestLine_AreSkipped()
    {
        HttpFileRequest request = ParseOne("# a comment\n// another\n\nGET http://h/");

        Assert.AreEqual("GET", request.Method);
        Assert.AreEqual("http://h/", request.Url);
    }

    [TestMethod]
    public void Parse_RequestLineWithoutAMethod_IsAGet()
    {
        HttpFileRequest request = ParseOne("http://h/path");

        Assert.AreEqual("GET", request.Method);
        Assert.AreEqual("http://h/path", request.Url);
    }

    [TestMethod]
    public void Parse_RequestLineOfOneWord_IsItsUrl() =>
        Assert.AreEqual("PUT", ParseOne("PUT").Url);

    [TestMethod]
    public void Parse_LowercaseFirstWord_IsNotAMethod() =>
        Assert.AreEqual("get", ParseOne("get http://h/").Url);

    [TestMethod]
    public void Parse_QueryLinesAfterTheRequestLine_AreAppendedToTheUrl() =>
        Assert.AreEqual("http://h/?a=1&b={{x}}", ParseOne("@v = {{x}}\nGET http://h/\n    ?a=1\n    &b={{v}}", Variables(("x", "{{x}}"))).Url);

    [TestMethod]
    public void Parse_HeadersUpToTheBlankLine_SkipCommentsAndSubstituteVariables()
    {
        HttpFileRequest request = ParseOne("@t = abc\nGET http://h/\nX-Token: {{t}}\n# not a header\nAccept: */*\n");

        CollectionAssert.AreEqual(new[] { "X-Token: abc", "Accept: */*" }, request.Headers.ToArray());
        Assert.IsNull(request.Body);
    }

    [TestMethod]
    public void Parse_HeaderLineWithoutAColon_FailsWithExit2()
    {
        ArgumentExpansionException failure = Assert.ThrowsExactly<ArgumentExpansionException>(() => ParseOne("GET http://h/\nAccept: */*\n?a=1"));

        Assert.AreEqual(CurlExitCode.FailedInit, failure.ExitCode);
        Assert.AreEqual("http-file: '?a=1' is not a header line", failure.Message);
    }

    [TestMethod]
    public void Parse_BodyWithTrailingBlankLines_DropsThem() =>
        Assert.AreEqual("a=1\n\nb=2", ParseOne("POST http://h/\n\na=1\n\nb=2\n\n  \n").Body);

    [TestMethod]
    public void Parse_BodyOfACrLfFile_KeepsCrLf() =>
        Assert.AreEqual("line1\r\nline2", ParseOne("POST http://h/\r\n\r\nline1\r\nline2\r\n").Body);

    [TestMethod]
    public void Parse_OnlyBlankLinesAfterTheHeaders_IsNoBody() =>
        Assert.IsNull(ParseOne("POST http://h/\n\n\n").Body);

    [TestMethod]
    public void Parse_BodyOfOneFileReference_NamesTheFile()
    {
        HttpFileRequest request = ParseOne("@dir = data\nPOST http://h/\n\n< ./{{dir}}/body.json");

        Assert.AreEqual("./data/body.json", request.BodyFile);
        Assert.IsNull(request.Body);
    }

    [TestMethod]
    public void Parse_FileReferenceFollowedByMoreLines_IsAnInlineBody() =>
        Assert.AreEqual("< a\nb", ParseOne("POST http://h/\n\n< a\nb").Body);

    [TestMethod]
    public void Parse_SeparatorsWithNoRequestBetween_GiveNoEmptyRequest()
    {
        IReadOnlyList<HttpFileRequest> requests = HttpFileParser.Parse("### first\nGET http://a/\n### second\n###\nGET http://b/\n###", Variables());

        CollectionAssert.AreEqual(new[] { "http://a/", "http://b/" }, requests.Select(request => request.Url).ToArray());
    }

    [TestMethod]
    public void Parse_VariableLineAfterTheRequestLine_IsAHeaderError() =>
        Assert.ThrowsExactly<ArgumentExpansionException>(() => ParseOne("GET http://h/\n@a = 1"));

    [TestMethod]
    public void Parse_AtSignWithoutEquals_IsARequestLine() =>
        Assert.AreEqual("@host", ParseOne("@host").Url);

    private static HttpFileRequest ParseOne(string text, HttpFileVariables? variables = null) =>
        HttpFileParser.Parse(text, variables ?? Variables()).Single();

    private static HttpFileVariables Variables(params (string Name, string Value)[] values)
    {
        HttpFileVariables variables = new(_ => null);
        foreach ((string name, string value) in values)
        {
            variables.Set(name, value);
        }

        return variables;
    }
}
