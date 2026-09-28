using System.Text;
using Curl.Protocol.Abstractions;

namespace Curl.Cli;

/// <summary>
/// Pins <c>-F</c>/<c>--form</c> and <c>--form-string</c>. Measured with the local curl 8.21.0 (mingw,
/// Schannel) on 2026-09-26 with <c>Record-CurlExchange.ps1 -Port 18189 -CurlArgs --no-progress-meter,
/// -F,&lt;value&gt;,http://127.0.0.1:18189/</c>, run in a folder holding <c>f.txt</c> (<c>hi</c>),
/// <c>g.txt</c> (<c>y</c>) and the header files below, reading the request body, standard error and
/// the exit code; refusals against <c>http://127.0.0.1:1/</c>. The part each case below expects is
/// the one the recorded body showed, for example <c>-F a=@f.txt</c> sent
/// <c>Content-Disposition: form-data; name="a"; filename="f.txt"</c>, <c>Content-Type: text/plain</c>
/// and <c>hi</c>, and <c>-F 'a=b;headers=X-A: 1'</c> sent <c>X-A: 1</c> after the disposition line.
/// </summary>
[TestClass]
public sealed class CommandLineFormOptionTests
{
    private const string Url = "http://example.com/";

    private const string TryHelp = "curl: try 'curl --help' or 'curl --manual' for more information";

    [TestMethod]
    [DataRow("-F", "a=b")]
    [DataRow("--form", "a=b")]
    public void Parse_NameAndValue_IsATextPart(string option, string value)
    {
        CommandLineParseResult result = Parse([option, value, Url]);

        AssertAccepted(result);
        AssertPart(result.Options!.FormParts.Single(), "a", FormPartKind.Text, "b");
    }

    [TestMethod]
    [DataRow("-Fa=b")]
    [DataRow("--form=a=b")]
    public void Parse_ValueJoinedToTheOption_IsATextPart(string argument)
    {
        CommandLineParseResult result = Parse([argument, Url]);

        AssertAccepted(result);
        AssertPart(result.Options!.FormParts.Single(), "a", FormPartKind.Text, "b");
    }

    [TestMethod]
    public void Parse_NoForm_LeavesTheFormEmpty()
    {
        CommandLineParseResult result = Parse([Url]);

        AssertAccepted(result);
        Assert.IsEmpty(result.Options!.FormParts);
    }

    [TestMethod]
    public void Parse_AtFile_IsAFileUploadPart()
    {
        CommandLineParseResult result = Parse(["-F", "a=@f.txt", Url]);

        AssertAccepted(result);
        AssertPart(result.Options!.FormParts.Single(), "a", FormPartKind.FileUpload, "f.txt");
    }

    [TestMethod]
    public void Parse_AtStandardInput_NamesTheDashAndReadsNothing()
    {
        RecordingDataFileReader reader = new() { StandardInput = [1] };

        CommandLineParseResult result = Parse(["-F", "a=@-;filename=s", Url], reader);

        AssertAccepted(result);
        AssertPart(result.Options!.FormParts.Single(), "a", FormPartKind.FileUpload, "-", fileName: "s");
        Assert.IsEmpty(reader.Reads);
    }

    [TestMethod]
    public void Parse_LessThanFile_IsAFileContentPart()
    {
        CommandLineParseResult result = Parse(["-F", "a=<f.txt", Url]);

        AssertAccepted(result);
        AssertPart(result.Options!.FormParts.Single(), "a", FormPartKind.FileContent, "f.txt");
    }

    [TestMethod]
    public void Parse_EmptyName_IsAPartWithNoName()
    {
        CommandLineParseResult result = Parse(["-F", "=b", Url]);

        AssertAccepted(result);
        AssertPart(result.Options!.FormParts.Single(), null, FormPartKind.Text, "b");
    }

    [TestMethod]
    public void Parse_SeveralValues_KeepCommandLineOrder()
    {
        CommandLineParseResult result = Parse(["-F", "a=b", "--form-string", "c=d", "-F", "e=@f.txt", Url]);

        AssertAccepted(result);
        CollectionAssert.AreEqual(new[] { "a", "c", "e" }, result.Options!.FormParts.Select(part => part.Name).ToArray());
    }

    [TestMethod]
    [DataRow("a=b;type=text/x", "b", "text/x")]
    [DataRow("a=b;TYPE=text/x", "b", "text/x")]
    [DataRow("a=b;type=text/plain; charset=utf-8", "b", "text/plain; charset=utf-8")]
    [DataRow("a=b;type=text/x;", "b", "text/x;")]
    [DataRow("a=b;;type=t/u", "b", "t/u")]
    [DataRow("a=b;type=", "b", "")]
    [DataRow("a=\"xy;type=t/u", "\"xy", "t/u")]
    public void Parse_Type_IsTheContentType(string value, string content, string contentType)
    {
        CommandLineParseResult result = Parse(["-F", value, Url]);

        AssertAccepted(result);
        AssertPart(result.Options!.FormParts.Single(), "a", FormPartKind.Text, content, contentType: contentType);
    }

    [TestMethod]
    public void Parse_TypeThenParametersThenFileName_KeepsTheParametersInTheType()
    {
        CommandLineParseResult result = Parse(["-F", "a=b;type=text/plain; charset=utf-8;filename=z", Url]);

        AssertAccepted(result);
        AssertPart(result.Options!.FormParts.Single(), "a", FormPartKind.Text, "b", contentType: "text/plain; charset=utf-8", fileName: "z");
    }

    [TestMethod]
    public void Parse_FileWithTypeAndFileName_KeepsBoth()
    {
        CommandLineParseResult result = Parse(["-F", "a=@f.txt;type=image/png;FileName=q", Url]);

        AssertAccepted(result);
        AssertPart(result.Options!.FormParts.Single(), "a", FormPartKind.FileUpload, "f.txt", contentType: "image/png", fileName: "q");
    }

    [TestMethod]
    public void Parse_FileTypeEndingAtAnEqualsSign_IgnoresTheRestWithoutAWarning()
    {
        CommandLineParseResult result = Parse(["-F", "a=@f.txt;type=a=g.txt", Url]);

        AssertAccepted(result);
        AssertPart(result.Options!.FormParts.Single(), "a", FormPartKind.FileUpload, "f.txt", contentType: "a");
    }

    [TestMethod]
    public void Parse_Encoder_IsKeptUnchecked()
    {
        CommandLineParseResult result = Parse(["-F", "a=b;encoder=base64", Url]);

        AssertAccepted(result);
        AssertPart(result.Options!.FormParts.Single(), "a", FormPartKind.Text, "b", encoder: "base64");
    }

    [TestMethod]
    public void Parse_Headers_AreThePartsOwnHeaders()
    {
        CommandLineParseResult result = Parse(["-F", "a=b;headers=X-A: 1;headers= \"Y: ;\"", Url]);

        AssertAccepted(result);
        CollectionAssert.AreEqual(new[] { "X-A: 1", "Y: ;" }, result.Options!.FormParts.Single().Headers.ToArray());
    }

    [TestMethod]
    [DataRow("a=b;headers=@h.txt")]
    [DataRow("a=b;headers=< h.txt ")]
    public void Parse_HeadersFile_AddsItsLines(string value)
    {
        RecordingDataFileReader reader = new();
        reader.Files["h.txt"] = Encoding.UTF8.GetBytes("X-H: 1\r\nX-J: 2\n");

        CommandLineParseResult result = Parse(["-F", value, Url], reader);

        AssertAccepted(result);
        CollectionAssert.AreEqual(new[] { "X-H: 1", "X-J: 2" }, result.Options!.FormParts.Single().Headers.ToArray());
    }

    [TestMethod]
    [DataRow("# c\r\nX-A: 1  \n  folded\n\nX-B:2\n", new[] { "X-A: 1  folded", "X-B:2" })]
    [DataRow("  lead\n\tX-T: 1\nX-U: 2\n   \n  more\n", new[] { "  lead", "\tX-T: 1", "X-U: 2  more" })]
    public void Parse_HeadersFile_SkipsCommentsAndBlankLinesAndFoldsSpaceIndentedLines(string contents, string[] headers)
    {
        RecordingDataFileReader reader = new();
        reader.Files["h.txt"] = Encoding.UTF8.GetBytes(contents);

        CommandLineParseResult result = Parse(["-F", "a=b;headers=@h.txt", Url], reader);

        AssertAccepted(result);
        CollectionAssert.AreEqual(headers, result.Options!.FormParts.Single().Headers.ToArray());
    }

    [TestMethod]
    public void Parse_HeadersFileThatCannotBeRead_WarnsAndAddsNoHeader()
    {
        CommandLineParseResult result = Parse(["-F", "a=b;headers=@nx.txt", Url]);

        AssertAccepted(result, "Warning: Cannot read from nx.txt: No such file or directory");
        Assert.IsEmpty(result.Options!.FormParts.Single().Headers);
    }

    [TestMethod]
    public void Parse_QuotedValue_KeepsSemicolonsAndUnescapes()
    {
        CommandLineParseResult result = Parse(["-F", "a=\"x\\\"y\\\\z;w\";filename=\"q\\\"r\"", Url]);

        AssertAccepted(result);
        AssertPart(result.Options!.FormParts.Single(), "a", FormPartKind.Text, "x\"y\\z;w", fileName: "q\"r");
    }

    [TestMethod]
    public void Parse_QuotedValueWithABackslashBeforeAnotherCharacter_KeepsTheBackslash()
    {
        CommandLineParseResult result = Parse(["-F", "a=\" x\\n \"", Url]);

        AssertAccepted(result);
        AssertPart(result.Options!.FormParts.Single(), "a", FormPartKind.Text, " x\\n ");
    }

    [TestMethod]
    public void Parse_TrailingDataAfterAQuote_WarnsAndIsSkipped()
    {
        CommandLineParseResult result = Parse(["-F", "a=\"x;y\" z", Url]);

        AssertAccepted(result, "Warning: Trailing data after quoted form parameter");
        AssertPart(result.Options!.FormParts.Single(), "a", FormPartKind.Text, "x;y");
    }

    [TestMethod]
    public void Parse_WhiteSpaceAfterAQuote_IsSkippedWithoutAWarning()
    {
        CommandLineParseResult result = Parse(["-F", "a=\"x\" \t;type=t/u", Url]);

        AssertAccepted(result);
        AssertPart(result.Options!.FormParts.Single(), "a", FormPartKind.Text, "x", contentType: "t/u");
    }

    [TestMethod]
    public void Parse_BlanksAroundUnquotedWords_AreDropped()
    {
        CommandLineParseResult result = Parse(["-F", "a=  b  ; type = text/x ;filename= n ", Url]);

        AssertAccepted(result, "Warning: skip unknown form field: type = text/x ");
        AssertPart(result.Options!.FormParts.Single(), "a", FormPartKind.Text, "b", fileName: "n");
    }

    [TestMethod]
    public void Parse_UnknownParameter_WarnsAndIsSkipped()
    {
        CommandLineParseResult result = Parse(["-F", "a=b;foo=x", Url]);

        AssertAccepted(result, "Warning: skip unknown form field: foo=x");
        AssertPart(result.Options!.FormParts.Single(), "a", FormPartKind.Text, "b");
    }

    [TestMethod]
    public void Parse_GarbageAfterTheType_Warns()
    {
        CommandLineParseResult result = Parse(["-F", "a=b;type=text/x y", Url]);

        AssertAccepted(result, "Warning: garbage at end of field specification:  y");
        AssertPart(result.Options!.FormParts.Single(), "a", FormPartKind.Text, "b", contentType: "text/x");
    }

    [TestMethod]
    public void Parse_FileNameOnAFileContentPart_WarnsAndIsDropped()
    {
        CommandLineParseResult result = Parse(["-F", "a=<f.txt;filename=z", Url]);

        AssertAccepted(result, "Warning: Field filename not allowed here: z");
        AssertPart(result.Options!.FormParts.Single(), "a", FormPartKind.FileContent, "f.txt");
    }

    [TestMethod]
    public void Parse_SeveralFiles_AreAMultipartOfFileUploads()
    {
        CommandLineParseResult result = Parse(["-F", "a=@f.txt;type=x/y,g.txt;filename=G", Url]);

        AssertAccepted(result);
        FormPartSpecification group = result.Options!.FormParts.Single();
        AssertPart(group, "a", FormPartKind.Multipart, string.Empty);
        Assert.HasCount(2, group.Parts);
        AssertPart(group.Parts[0], null, FormPartKind.FileUpload, "f.txt", contentType: "x/y");
        AssertPart(group.Parts[1], null, FormPartKind.FileUpload, "g.txt", fileName: "G");
    }

    [TestMethod]
    public void Parse_OpenedMultipart_TakesThePartsUntilClosed()
    {
        CommandLineParseResult result = Parse(["-F", "a=(;type=multipart/mixed", "-F", "b=c", "-F", "=)", "-F", "d=e", Url]);

        AssertAccepted(result);
        IReadOnlyList<FormPartSpecification> parts = result.Options!.FormParts;
        Assert.HasCount(2, parts);
        AssertPart(parts[0], "a", FormPartKind.Multipart, string.Empty, contentType: "multipart/mixed");
        AssertPart(parts[0].Parts.Single(), "b", FormPartKind.Text, "c");
        AssertPart(parts[1], "d", FormPartKind.Text, "e");
    }

    [TestMethod]
    public void Parse_MultipartNeverClosed_IsAccepted()
    {
        CommandLineParseResult result = Parse(["-F", "a=(", "-F", "b=(", "-F", "c=d", Url]);

        AssertAccepted(result);
        AssertPart(result.Options!.FormParts.Single().Parts.Single().Parts.Single(), "c", FormPartKind.Text, "d");
    }

    [TestMethod]
    public void Parse_MultipartWithFileNameAndEncoder_WarnsAndKeepsTheHeaders()
    {
        CommandLineParseResult result = Parse(["-F", "a=(;filename=z;encoder=7bit;headers=X: 1", Url]);

        AssertAccepted(result, "Warning: Field filename not allowed here: z", "Warning: Field encoder not allowed here: 7bit");
        FormPartSpecification multipart = result.Options!.FormParts.Single();
        AssertPart(multipart, "a", FormPartKind.Multipart, string.Empty);
        CollectionAssert.AreEqual(new[] { "X: 1" }, multipart.Headers.ToArray());
    }

    [TestMethod]
    public void Parse_NamedCloseParenthesis_IsATextPart()
    {
        CommandLineParseResult result = Parse(["-F", "x=)", Url]);

        AssertAccepted(result);
        AssertPart(result.Options!.FormParts.Single(), "x", FormPartKind.Text, ")");
    }

    [TestMethod]
    [DataRow("a=@f.txt;type=x")]
    [DataRow("a=<f.txt")]
    [DataRow("a=(")]
    [DataRow("a=\"b\";x")]
    public void Parse_FormString_TakesTheWholeValueAsText(string value)
    {
        CommandLineParseResult result = Parse(["--form-string", value, Url]);

        AssertAccepted(result);
        AssertPart(result.Options!.FormParts.Single(), "a", FormPartKind.Text, value[2..]);
    }

    [TestMethod]
    public void Parse_FormStringCloseParenthesis_IsATextPartWithNoName()
    {
        CommandLineParseResult result = Parse(["-F", "a=(", "--form-string", "=)", Url]);

        AssertAccepted(result);
        AssertPart(result.Options!.FormParts.Single().Parts.Single(), null, FormPartKind.Text, ")");
    }

    [TestMethod]
    [DataRow("-F", "abc")]
    [DataRow("-F", "")]
    [DataRow("--form-string", "abc")]
    public void Parse_ValueWithNoEqualsSign_IsRefused(string option, string value)
    {
        CommandLineParseResult result = Parse([option, value, Url]);

        AssertRefused(result, ["Warning: Illegally formatted input field"], $"curl: option {option}: is badly used here", TryHelp);
    }

    [TestMethod]
    public void Parse_EmptyJoinedValue_IsRefusedNamingTheOptionAsTyped()
    {
        CommandLineParseResult result = Parse(["--form=", Url]);

        AssertRefused(result, ["Warning: Illegally formatted input field"], "curl: option --form=: is badly used here", TryHelp);
    }

    [TestMethod]
    public void Parse_ValueWithNoEqualsSignAfterSilent_DropsTheWarning()
    {
        CommandLineParseResult result = Parse(["-s", "-F", "abc", Url]);

        AssertRefused(result, [], "curl: option -F: is badly used here", TryHelp);
    }

    [TestMethod]
    public void Parse_CloseWithNoMultipartOpen_IsRefused()
    {
        CommandLineParseResult result = Parse(["-F", "=)", Url]);

        AssertRefused(result, ["Warning: no multipart to terminate"], "curl: option -F: is badly used here", TryHelp);
    }

    [TestMethod]
    public void Parse_SecondCloseAfterTheMultipartIsClosed_IsRefused()
    {
        CommandLineParseResult result = Parse(["-F", "a=(", "-F", "=)", "-F", "=)", Url]);

        AssertRefused(result, ["Warning: no multipart to terminate"], "curl: option -F: is badly used here", TryHelp);
    }

    [TestMethod]
    [DataRow("-I", "-F", "multipart formpost (-F, --form) and HEAD (-I, --head).", "-F")]
    [DataRow("--no-head", "-F", "multipart formpost (-F, --form) and GET (-G, --get).", "-F")]
    [DataRow("-F", "-I", "HEAD (-I, --head) and multipart formpost (-F, --form).", "-I")]
    [DataRow("-F", "--no-head", "GET (-G, --get) and multipart formpost (-F, --form).", "--no-head")]
    public void Parse_FormAndAnotherMethod_RefusesTheSecond(string first, string second, string methods, string refused)
    {
        string[] arguments = first == "-F" ? [first, "a=b", second, Url] : [first, second, "a=b", Url];

        CommandLineParseResult result = Parse(arguments);

        AssertRefused(
            result,
            [$"Warning: You can only select one HTTP request method! You asked for both {methods}"],
            $"curl: option {refused}: is badly used here",
            TryHelp);
    }

    [TestMethod]
    public void Parse_FormThenHeadAfterSilent_DropsTheWarning()
    {
        CommandLineParseResult result = Parse(["-s", "-F", "a=b", "-I", Url]);

        AssertRefused(result, [], "curl: option -I: is badly used here", TryHelp);
    }

    [TestMethod]
    [DataRow("-F", "a=b", "-d", "x")]
    [DataRow("-d", "x", "-F", "a=b")]
    [DataRow("-F", "a=b", "--data-raw", "x")]
    [DataRow("-F", "a=b", "--data-urlencode", "x")]
    [DataRow("-F", "a=b", "--data-binary", "x")]
    [DataRow("--data-ascii", "x", "-F", "a=b")]
    [DataRow("-F", "a=b", "--json", "{}")]
    [DataRow("--form-string", "a=b", "-d", "")]
    public void Parse_FormAndData_IsRefusedOnceReadWithTheWarningAlone(string firstOption, string firstValue, string secondOption, string secondValue)
    {
        CommandLineParseResult result = Parse([firstOption, firstValue, secondOption, secondValue, Url]);

        AssertRefusedAtTransferSetup(
            result,
            [
                "Warning: You can only select one HTTP request method! You asked for both POST (-d, --data) and multipart formpost (-F, --form).",
            ]);
    }

    [TestMethod]
    public void Parse_FormAndDataSentAsTheQuery_NamesGet()
    {
        CommandLineParseResult result = Parse(["-F", "a=b", "-d", "x", "-G", Url]);

        AssertRefusedAtTransferSetup(
            result,
            [
                "Warning: You can only select one HTTP request method! You asked for both GET (-G, --get) and multipart formpost (-F, --form).",
            ]);
    }

    [TestMethod]
    [DataRow("-s")]
    [DataRow("-sS")]
    public void Parse_FormAndDataWithSilentAnywhere_PrintsNothing(string silent)
    {
        CommandLineParseResult result = Parse(["-F", "a=b", "-d", "x", silent, Url]);

        AssertRefusedAtTransferSetup(result, []);
    }

    [TestMethod]
    public void Parse_FormAndGetWithNoData_IsAccepted()
    {
        CommandLineParseResult result = Parse(["-F", "a=b", "-G", Url]);

        AssertAccepted(result);
    }

    [TestMethod]
    public void Parse_FormAndDataWithNoUrl_ReportsTheMissingUrl()
    {
        CommandLineParseResult result = Parse(["-F", "a=b", "-d", "x"]);

        AssertRefused(result, [], "curl: (2) no URL specified", TryHelp);
    }

    [TestMethod]
    [DataRow("--no-form")]
    [DataRow("--no-form=x")]
    [DataRow("--no-form-string")]
    [DataRow("--no-form-string=a=b")]
    public void Parse_NoPrefix_CannotBeReversed(string argument)
    {
        CommandLineParseResult result = Parse([argument, Url]);

        AssertRefused(result, [], $"curl: option {argument}: the given option cannot be reversed with a --no- prefix", TryHelp);
    }

    private static void AssertPart(FormPartSpecification part, string? name, FormPartKind kind, string content, string? contentType = null, string? fileName = null, string? encoder = null)
    {
        Assert.AreEqual(name, part.Name);
        Assert.AreEqual(kind, part.Kind);
        Assert.AreEqual(content, part.Content);
        Assert.AreEqual(contentType, part.ContentType);
        Assert.AreEqual(fileName, part.FileName);
        Assert.AreEqual(encoder, part.Encoder);
    }

    private static void AssertAccepted(CommandLineParseResult result, params string[] warningLines)
    {
        Assert.IsTrue(result.IsAccepted, result.Refusal is null ? string.Empty : string.Join('\n', result.Refusal.StandardErrorLines));
        CollectionAssert.AreEqual(warningLines, result.WarningLines.ToArray());
    }

    private static void AssertRefused(CommandLineParseResult result, string[] warningLines, params string[] refusalLines)
    {
        Assert.IsFalse(result.IsAccepted);
        Assert.AreEqual(CurlExitCode.FailedInit, result.Refusal!.ExitCode);
        CollectionAssert.AreEqual(warningLines, result.WarningLines.ToArray());
        CollectionAssert.AreEqual(refusalLines, result.Refusal.StandardErrorLines.ToArray());
    }

    private static void AssertRefusedAtTransferSetup(CommandLineParseResult result, string[] refusalLines)
    {
        Assert.IsFalse(result.IsAccepted);
        Assert.AreEqual(CurlExitCode.FailedInit, result.Refusal!.ExitCode);
        Assert.IsTrue(result.Refusal.FoundAtTransferSetup);
        Assert.IsEmpty(result.WarningLines);
        CollectionAssert.AreEqual(refusalLines, result.Refusal.StandardErrorLines.ToArray());
    }

    private static CommandLineParseResult Parse(IReadOnlyList<string> arguments) =>
        Parse(arguments, new RecordingDataFileReader());

    private static CommandLineParseResult Parse(IReadOnlyList<string> arguments, IDataFileReader reader) =>
        CommandLineParser.Parse(arguments, _ => true, new UnexpectedPasswordPrompt(), reader);

    private sealed class UnexpectedPasswordPrompt : IPasswordPrompt
    {
        public string ReadPassword(string prompt) => throw new AssertFailedException("No password prompt was expected.");
    }
}
