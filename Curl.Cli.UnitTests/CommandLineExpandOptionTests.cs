using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Cli;

/// <summary>
/// Pins <c>--expand-&lt;option&gt;</c> and the <c>{{name}}</c> functions. Measured with the local curl 8.21.0
/// (mingw, Schannel) on 2026-09-27, sending <c>--expand-data</c> to a loopback server that echoes the
/// request body and reading the bytes back: <c>a=hello</c> gives <c>[hello]</c>; <c>trim</c> of
/// <c>0D 0A 09 20 x y 20 0B 0C 0D 0A</c> gives <c>x y</c>; <c>json</c> of <c>a"b\c/d</c> gives <c>a\"b\\c/d</c> and of
/// <c>a 08 b 0C c 09 d 01 e 7F f \ g " h /</c> gives <c>a\bb\fc\td\u0001e 7F f\\g\"h/</c>, passing <c>C3 A9</c>
/// through; <c>url</c> of <c>a b&amp;c=d~._-</c> gives <c>a%20b%26c%3Dd~._-</c> and of <c>C3 A9 20 2B</c>
/// <c>%C3%A9%20%2B</c>; <c>b64</c> of <c>hello</c> gives <c>aGVsbG8=</c>; <c>64dec</c> of <c>aGVsbG8=</c> gives
/// <c>hello</c>, of <c>QQ==</c>, <c>QR==</c> and <c>QQ==</c>-NUL-<c>junk</c> <c>A</c>, of <c>QUI=</c> <c>AB</c>, of
/// <c>QUJD</c> <c>ABC</c>, of <c>QUJDRA==</c> <c>ABCD</c>, and of <c>aGVsbG8</c>, <c>!!!!</c>, <c>Q===</c>, <c>Q=Q=</c>,
/// <c>QQ=A</c>, <c>QUJ</c>, <c>QU JD</c>, <c>-_==</c> and <c>QUJDRA=A</c> <c>[64dec-fail]</c>; <c>trim:b64:64dec:url</c>
/// of <c> x </c> gives <c>x</c>; an unset variable gives nothing, with or without functions. An unknown
/// function (<c>bogus</c>, <c>trimx</c>, an empty one) exits 2 with <c>curl: unknown variable function in
/// '&lt;functions&gt;'</c> and <c>curl: option --expand-data: variable expansion failure</c>, the error line hidden
/// by <c>-s</c> unless <c>-S</c> follows; a NUL byte left after the functions exits 2 with
/// <c>curl: variable contains null byte</c>. <c>[{{}}]</c> and <c>[{{:trim}}]</c> are kept with
/// <c>Warning: bad variable name length '&lt;value&gt;'</c>, <c>[{{a-b}}]</c> with <c>Warning: bad variable name:
/// a-b</c>, a 128-letter name with the length warning wrapped over three lines; <c>[{{a</c> is kept with
/// <c>Warning: missing close '}}' in '[{{a'</c>; <c>\{{a}} {{a}}</c> gives <c>{{a}} x</c> while <c>\{{a}}</c>
/// alone is kept as written. <c>--expand-silent x</c>, <c>--expand-silent=x</c> and a <c>-K</c> line
/// <c>expand-silent = x</c> exit 2 with <c>variable expansion failure</c>; <c>--no-expand-data</c>,
/// <c>--expand-no-silent</c> and <c>--expand-bogus</c> are unknown.
/// </summary>
[TestClass]
public sealed class CommandLineExpandOptionTests
{
    private const string Url = "http://example.com/";

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    [DataRow("[{{a}}]", "[hello]")]
    [DataRow("{{a}}{{a}}", "hellohello")]
    [DataRow("[{{nope}}]", "[]")]
    [DataRow("[{{nope:b64}}]", "[]")]
    [DataRow("[{{nope:trim}}]", "[]")]
    [DataRow("[{{nope:json:url:64dec}}]", "[]")]
    [DataRow("{{a}} {{b", "hello {{b")]
    [DataRow("\\{{a}} {{a}}", "{{a}} hello")]
    [DataRow("{{a}}\\{{a}} tail", "hello{{a}} tail")]
    [DataRow("\\{{a}}", "\\{{a}}")]
    [DataRow("p\\{{a}} q", "p\\{{a}} q")]
    [DataRow("no references", "no references")]
    [DataRow("{{a:trim}x}}", "hello")]
    [DataRow("[{{a-b}}] {{c}}", "[{{a-b}}] ")]
    [DataRow("{{a:b64}}", "aGVsbG8=")]
    [DataRow("{{a:b64:64dec}}", "hello")]
    public void Parse_ExpandedValue_ReplacesEachReferenceAsCurlDoes(string template, string expected)
    {
        CommandLineParseResult result = Parse(["--variable", "a=hello", "--expand-user-agent", template, Url]);

        Diagnostics.Assert("accepted", true, result.IsAccepted);
        Diagnostics.Assert("user agent", expected, result.Options?.UserAgent);
        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual(expected, result.Options!.UserAgent);
    }

    [TestMethod]
    [DataRow(new byte[] { 0x0D, 0x0A, 0x09, 0x20, 0x78, 0x20, 0x79, 0x20, 0x0B, 0x0C, 0x0D, 0x0A }, "trim", new byte[] { 0x78, 0x20, 0x79 })]
    [DataRow(new byte[] { 0x20, 0x20 }, "trim", new byte[0])]
    [DataRow(new byte[] { 0x61, 0x08, 0x62, 0x0C, 0x63, 0x09, 0x64, 0x01, 0x65, 0x7F, 0x66, 0x5C, 0x67, 0x22, 0x68, 0x2F, 0x0A, 0x0D }, "json", new byte[] { 0x61, 0x5C, 0x62, 0x62, 0x5C, 0x66, 0x63, 0x5C, 0x74, 0x64, 0x5C, 0x75, 0x30, 0x30, 0x30, 0x31, 0x65, 0x7F, 0x66, 0x5C, 0x5C, 0x67, 0x5C, 0x22, 0x68, 0x2F, 0x5C, 0x6E, 0x5C, 0x72 })]
    [DataRow(new byte[] { 0xC3, 0xA9, 0x20, 0x2B }, "json", new byte[] { 0xC3, 0xA9, 0x20, 0x2B })]
    [DataRow(new byte[] { 0xC3, 0xA9, 0x20, 0x2B, 0x7E, 0x2E, 0x5F, 0x2D, 0x41, 0x7A, 0x30 }, "url", new byte[] { 0x25, 0x43, 0x33, 0x25, 0x41, 0x39, 0x25, 0x32, 0x30, 0x25, 0x32, 0x42, 0x7E, 0x2E, 0x5F, 0x2D, 0x41, 0x7A, 0x30 })]
    [DataRow(new byte[] { 0x61, 0x00, 0x62 }, "b64", new byte[] { 0x59, 0x51, 0x42, 0x69 })]
    [DataRow(new byte[] { 0x51, 0x51, 0x3D, 0x3D, 0x00, 0x6A }, "64dec", new byte[] { 0x41 })]
    [DataRow(new byte[] { 0x00, 0x51 }, "64dec", new byte[] { 0x5B, 0x36, 0x34, 0x64, 0x65, 0x63, 0x2D, 0x66, 0x61, 0x69, 0x6C, 0x5D })]
    [DataRow(new byte[] { 0x20, 0x78, 0x20 }, "trim:b64:64dec:url", new byte[] { 0x78 })]
    public void Parse_ExpandedFunction_TransformsTheVariablesBytesAsCurlDoes(byte[] content, string functions, byte[] expected)
    {
        RecordingDataFileReader reader = new() { Files = { ["v.bin"] = content } };

        Diagnostics.Bytes("v.bin", content);
        CommandLineParseResult result = Parse(["--variable", "a@v.bin", "--expand-data", $"{{{{a:{functions}}}}}", Url], reader);

        Diagnostics.Assert("accepted", true, result.IsAccepted);
        Diagnostics.Diff("post data", expected, result.Options?.PostData?.ToArray() ?? []);
        Assert.IsTrue(result.IsAccepted);
        CollectionAssert.AreEqual(expected, result.Options!.PostData!.Value.ToArray());
    }

    [TestMethod]
    [DataRow("aGVsbG8=", "hello")]
    [DataRow("QQ==", "A")]
    [DataRow("QR==", "A")]
    [DataRow("QUI=", "AB")]
    [DataRow("QUJD", "ABC")]
    [DataRow("QUJDRA==", "ABCD")]
    [DataRow("aGVsbG8", "[64dec-fail]")]
    [DataRow("!!!!", "[64dec-fail]")]
    [DataRow("Q===", "[64dec-fail]")]
    [DataRow("====", "[64dec-fail]")]
    [DataRow("Q=Q=", "[64dec-fail]")]
    [DataRow("QQ=A", "[64dec-fail]")]
    [DataRow("QUJ", "[64dec-fail]")]
    [DataRow("QU JD", "[64dec-fail]")]
    [DataRow("-_==", "[64dec-fail]")]
    [DataRow("QUJDRA=A", "[64dec-fail]")]
    [DataRow("Q=JDRA==", "[64dec-fail]")]
    [DataRow("QUJD!A==", "[64dec-fail]")]
    public void Parse_ExpandedBase64Decode_DecodesAsStrictlyAsCurl(string content, string expected)
    {
        CommandLineParseResult result = Parse(["--variable", $"a={content}", "--expand-user-agent", "{{a:64dec}}", Url]);

        Diagnostics.Assert("user agent", expected, result.Options?.UserAgent);
        Assert.AreEqual(expected, result.Options!.UserAgent);
    }

    [TestMethod]
    [DataRow("[{{a:bogus}}]", ":bogus")]
    [DataRow("[{{a:trim:bogus}}]", ":trim:bogus")]
    [DataRow("[{{a:}}]", ":")]
    [DataRow("[{{a:trim:}}]", ":trim:")]
    [DataRow("[{{a:trimx}}]", ":trimx")]
    [DataRow("[{{a::trim}}]", "::trim")]
    [DataRow("[{{a:tri}}]", ":tri")]
    [DataRow("[{{nope:bogus}}]", ":bogus")]
    public void Parse_ExpandedUnknownFunction_IsRefusedAsAnExpansionFailure(string template, string functions)
    {
        CommandLineParseResult result = Parse(["--variable", "a=x", "--expand-data", template, Url]);

        Diagnostics.Assert("exit code", CurlExitCode.FailedInit, result.Refusal?.ExitCode);
        AssertStandardErrorLines(
            [$"curl: unknown variable function in '{functions}'", "curl: option --expand-data: variable expansion failure", CommandLineRefusal.TryHelpLine],
            result);
        Assert.AreEqual(CurlExitCode.FailedInit, result.Refusal!.ExitCode);
        CollectionAssert.AreEqual(
            new[]
            {
                $"curl: unknown variable function in '{functions}'",
                "curl: option --expand-data: variable expansion failure",
                CommandLineRefusal.TryHelpLine,
            },
            result.Refusal.StandardErrorLines.ToArray());
    }

    [TestMethod]
    public void Parse_ExpandedUnknownFunctionAfterSilent_HidesTheErrorLineUnlessShowError()
    {
        CommandLineParseResult silent = Parse(["-s", "--variable", "a=x", "--expand-data", "{{a:bogus}}", Url]);
        CommandLineParseResult shown = Parse(["-s", "-S", "--variable", "a=x", "--expand-data", "{{a:bogus}}", Url]);

        AssertStandardErrorLines(["curl: option --expand-data: variable expansion failure", CommandLineRefusal.TryHelpLine], silent);
        Diagnostics.Assert("shown stderr line count", 3, shown.Refusal?.StandardErrorLines.Count);
        CollectionAssert.AreEqual(
            new[] { "curl: option --expand-data: variable expansion failure", CommandLineRefusal.TryHelpLine },
            silent.Refusal!.StandardErrorLines.ToArray());
        Assert.HasCount(3, shown.Refusal!.StandardErrorLines);
    }

    [TestMethod]
    public void Parse_ExpandedVariableHoldingANulByte_IsRefusedUnlessAFunctionEncodesIt()
    {
        RecordingDataFileReader reader = new() { Files = { ["nul.bin"] = [0x61, 0x00, 0x62] } };

        CommandLineParseResult refused = Parse(["--variable", "a@nul.bin", "--expand-data", "{{a}}", Url], reader);
        CommandLineParseResult unused = Parse(["--variable", "a@nul.bin", "--expand-data", "x", Url], reader);

        AssertStandardErrorLines(
            ["curl: variable contains null byte", "curl: option --expand-data: variable expansion failure", CommandLineRefusal.TryHelpLine],
            refused);
        Diagnostics.Assert("unused accepted", true, unused.IsAccepted);
        CollectionAssert.AreEqual(
            new[] { "curl: variable contains null byte", "curl: option --expand-data: variable expansion failure", CommandLineRefusal.TryHelpLine },
            refused.Refusal!.StandardErrorLines.ToArray());
        Assert.IsTrue(unused.IsAccepted);
    }

    [TestMethod]
    [DataRow("[{{}}]", "Warning: bad variable name length '[{{}}]'")]
    [DataRow("[{{:trim}}]", "Warning: bad variable name length '[{{:trim}}]'")]
    [DataRow("[{{a-b}}]", "Warning: bad variable name: a-b")]
    [DataRow("[{{a", "Warning: missing close '}}' in '[{{a'")]
    public void Parse_ExpandedReferenceThatIsNotAName_IsKeptAsWrittenWithCurlsWarning(string template, string warning)
    {
        CommandLineParseResult result = Parse(["--expand-user-agent", template, Url]);

        Diagnostics.Assert("accepted", true, result.IsAccepted);
        Diagnostics.Assert("user agent", template, result.Options?.UserAgent);
        AssertWarningLines([warning], result);
        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual(template, result.Options!.UserAgent);
        CollectionAssert.AreEqual(new[] { warning }, result.WarningLines.ToArray());
    }

    [TestMethod]
    public void Parse_ExpandedReferenceWithA128LetterName_IsKeptWithTheWarningWrappedAsCurlWrapsIt()
    {
        string name = new('x', 128);
        string template = $"{{{{{name}}}}}";

        CommandLineParseResult result = Parse(["--expand-user-agent", template, Url]);

        string[] expectedWarnings =
        [
            "Warning: bad variable name length ",
            "Warning: '{{" + new string('x', 67),
            "Warning: " + new string('x', 61) + "}}'",
        ];
        Diagnostics.Assert("user agent", template, result.Options?.UserAgent);
        AssertWarningLines(expectedWarnings, result);
        Assert.AreEqual(template, result.Options!.UserAgent);
        CollectionAssert.AreEqual(
            new[]
            {
                "Warning: bad variable name length ",
                "Warning: '{{" + new string('x', 67),
                "Warning: " + new string('x', 61) + "}}'",
            },
            result.WarningLines.ToArray());
    }

    [TestMethod]
    public void Parse_ExpandedReferenceWith127LetterName_IsExpanded()
    {
        string name = new('x', 127);

        CommandLineParseResult result = Parse(["--expand-user-agent", $"[{{{{{name}}}}}]", Url]);

        Diagnostics.Assert("user agent", "[]", result.Options?.UserAgent);
        AssertWarningLines([], result);
        Assert.AreEqual("[]", result.Options!.UserAgent);
        Assert.IsEmpty(result.WarningLines);
    }

    [TestMethod]
    public void Parse_ExpansionWarningAfterSilent_IsHidden()
    {
        CommandLineParseResult result = Parse(["-s", "--expand-user-agent", "[{{a", Url]);

        AssertWarningLines([], result);
        Assert.IsEmpty(result.WarningLines);
    }

    [TestMethod]
    public void Parse_ExpandedValueAttachedWithEquals_IsExpanded()
    {
        CommandLineParseResult result = Parse(["--variable=a=v", "--expand-user-agent=[{{a}}]", Url]);

        Diagnostics.Assert("user agent", "[v]", result.Options?.UserAgent);
        Assert.AreEqual("[v]", result.Options!.UserAgent);
    }

    [TestMethod]
    public void Parse_ExpandedVariable_DefinesAVariableFromOthers()
    {
        CommandLineParseResult result = Parse(["--variable", "a=1", "--expand-variable", "b={{a}}{{a}}", "--expand-user-agent", "[{{b}}]", Url]);

        Diagnostics.Assert("user agent", "[11]", result.Options?.UserAgent);
        Assert.AreEqual("[11]", result.Options!.UserAgent);
    }

    [TestMethod]
    public void Parse_ExpandedData_IsReadAsDataAfterExpansion()
    {
        RecordingDataFileReader reader = new() { Files = { ["body.txt"] = "body"u8.ToArray() } };

        CommandLineParseResult result = Parse(["--variable", "f=body.txt", "--expand-data", "@{{f}}", Url], reader);

        Diagnostics.Diff("post data", "body"u8.ToArray(), result.Options?.PostData?.ToArray() ?? []);
        CollectionAssert.AreEqual("body"u8.ToArray(), result.Options!.PostData!.Value.ToArray());
    }

    [TestMethod]
    public void Parse_ExpandedValueWithoutVariables_IsTheValueAsGiven()
    {
        CommandLineParseResult result = Parse(["--expand-url", "http://example.com/{{nope}}"]);

        Diagnostics.Assert("urls", CommandLineParseDiagnostics.QuoteEach(["http://example.com/"]), CommandLineParseDiagnostics.QuoteEach(result.Options?.Urls ?? []));
        CollectionAssert.AreEqual(new[] { "http://example.com/" }, result.Options!.Urls.ToArray());
    }

    [TestMethod]
    [DataRow("--expand-silent", "x")]
    [DataRow("--expand-silent=x", null)]
    [DataRow("--expand-silent=", null)]
    public void Parse_ExpandedFlagGivenAValue_IsRefusedAsAnExpansionFailure(string argument, string? next)
    {
        string[] arguments = next is null ? [argument, Url] : [argument, next, Url];

        CommandLineParseResult result = Parse(arguments);

        Diagnostics.Assert("exit code", CurlExitCode.FailedInit, result.Refusal?.ExitCode);
        AssertStandardErrorLines([$"curl: option {argument}: variable expansion failure", CommandLineRefusal.TryHelpLine], result);
        Assert.AreEqual(CurlExitCode.FailedInit, result.Refusal!.ExitCode);
        CollectionAssert.AreEqual(
            new[] { $"curl: option {argument}: variable expansion failure", CommandLineRefusal.TryHelpLine },
            result.Refusal.StandardErrorLines.ToArray());
    }

    [TestMethod]
    public void Parse_ExpandedFlagFollowedByANullArgument_IsRefusedAsGivenAnEmptyValue()
    {
        CommandLineParseResult result = Parse(["--expand-silent", null!, Url]);

        Diagnostics.Assert("exit code", CurlExitCode.FailedInit, result.Refusal?.ExitCode);
        AssertStandardErrorLines(["curl: option --expand-silent: variable expansion failure", CommandLineRefusal.TryHelpLine], result);
        Assert.AreEqual(CurlExitCode.FailedInit, result.Refusal!.ExitCode);
        CollectionAssert.AreEqual(
            new[] { "curl: option --expand-silent: variable expansion failure", CommandLineRefusal.TryHelpLine },
            result.Refusal.StandardErrorLines.ToArray());
    }

    [TestMethod]
    public void Parse_ExpandedFlagAsTheLastArgument_IsApplied()
    {
        CommandLineParseResult result = Parse([Url, "--expand-silent"]);

        Diagnostics.Assert("accepted", true, result.IsAccepted);
        Diagnostics.Assert("silent", true, result.Options?.Silent);
        Assert.IsTrue(result.IsAccepted);
        Assert.IsTrue(result.Options!.Silent);
    }

    [TestMethod]
    public void Parse_ExpandedValueOptionAsTheLastArgument_RequiresAParameter()
    {
        CommandLineParseResult result = Parse([Url, "--expand-data"]);

        Diagnostics.Assert("first stderr line", "curl: option --expand-data: requires parameter", result.Refusal?.StandardErrorLines.FirstOrDefault());
        Assert.AreEqual("curl: option --expand-data: requires parameter", result.Refusal!.StandardErrorLines[0]);
    }

    [TestMethod]
    [DataRow("--no-expand-data")]
    [DataRow("--expand-no-silent")]
    [DataRow("--expand-bogus")]
    [DataRow("--expand-")]
    public void Parse_ExpandedUnknownOption_IsRefusedAsUnknown(string argument)
    {
        CommandLineParseResult result = Parse([argument, "x", Url]);

        Diagnostics.Assert("first stderr line", $"curl: option {argument}: is unknown", result.Refusal?.StandardErrorLines.FirstOrDefault());
        Assert.AreEqual($"curl: option {argument}: is unknown", result.Refusal!.StandardErrorLines[0]);
    }

    [TestMethod]
    public void Parse_ExpandedFlagInAConfigFileWithAParameter_IsRefusedThroughTheConfigFile()
    {
        RecordingDataFileReader reader = new() { Files = { ["k3.cfg"] = "expand-silent = x\n"u8.ToArray() } };

        CommandLineParseResult result = Parse(["-K", "k3.cfg", Url], reader);

        AssertStandardErrorLines(
            [
                "curl: k3.cfg:1 config file option 'expand-silent' variable expansion failure",
                "curl: option -K: variable expansion failure",
                CommandLineRefusal.TryHelpLine,
            ],
            result);
        CollectionAssert.AreEqual(
            new[]
            {
                "curl: k3.cfg:1 config file option 'expand-silent' variable expansion failure",
                "curl: option -K: variable expansion failure",
                CommandLineRefusal.TryHelpLine,
            },
            result.Refusal!.StandardErrorLines.ToArray());
    }

    [TestMethod]
    public void Parse_ExpandedUnknownFunctionInAConfigFile_IsRefusedThroughTheConfigFile()
    {
        RecordingDataFileReader reader = new() { Files = { ["k2.cfg"] = "variable = a=hi\nexpand-data = \"[{{a:nope}}]\"\n"u8.ToArray() } };

        CommandLineParseResult result = Parse(["-K", "k2.cfg", Url], reader);

        AssertStandardErrorLines(
            [
                "curl: unknown variable function in ':nope'",
                "curl: k2.cfg:2 config file option 'expand-data' variable expansion failure",
                "curl: option -K: variable expansion failure",
                CommandLineRefusal.TryHelpLine,
            ],
            result);
        CollectionAssert.AreEqual(
            new[]
            {
                "curl: unknown variable function in ':nope'",
                "curl: k2.cfg:2 config file option 'expand-data' variable expansion failure",
                "curl: option -K: variable expansion failure",
                CommandLineRefusal.TryHelpLine,
            },
            result.Refusal!.StandardErrorLines.ToArray());
    }

    [TestMethod]
    public void Parse_ExpandedFlagInAConfigFileWithoutAParameter_IsApplied()
    {
        RecordingDataFileReader reader = new() { Files = { ["k1.cfg"] = "expand-silent\n"u8.ToArray() } };

        CommandLineParseResult result = Parse(["-K", "k1.cfg", Url], reader);

        Diagnostics.Assert("silent", true, result.Options?.Silent);
        Assert.IsTrue(result.Options!.Silent);
    }

    private CommandLineParseResult Parse(IReadOnlyList<string> arguments) =>
        Parse(arguments, new RecordingDataFileReader());

    private CommandLineParseResult Parse(IReadOnlyList<string> arguments, IDataFileReader reader)
    {
        Diagnostics.ArrangeArguments(arguments);
        CommandLineParseResult result = CommandLineParser.Parse(arguments, _ => false, new UnexpectedPasswordPrompt(), reader);
        Diagnostics.ActParse(result);
        return result;
    }

    private void AssertStandardErrorLines(string[] expected, CommandLineParseResult result) =>
        Diagnostics.Assert(
            "stderr lines",
            CommandLineParseDiagnostics.QuoteEach(expected),
            CommandLineParseDiagnostics.QuoteEach(result.Refusal?.StandardErrorLines ?? []));

    private void AssertWarningLines(string[] expected, CommandLineParseResult result) =>
        Diagnostics.Assert(
            "warning lines",
            CommandLineParseDiagnostics.QuoteEach(expected),
            CommandLineParseDiagnostics.QuoteEach(result.WarningLines));

    private sealed class UnexpectedPasswordPrompt : IPasswordPrompt
    {
        public string ReadPassword(string prompt) => throw new AssertFailedException("No password prompt was expected.");
    }
}
