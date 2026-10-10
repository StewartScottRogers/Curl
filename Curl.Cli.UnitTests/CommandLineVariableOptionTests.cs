using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Cli;

/// <summary>
/// Pins <c>--variable</c>. Measured with the local curl 8.21.0 (mingw, Schannel) on 2026-09-27 from
/// PowerShell, reading each variable back through <c>--expand-data '[{{a}}]'</c> sent to a loopback
/// server that echoes the request body: <c>a[1-3]=abcdef</c> gives <c>bcd</c>, <c>a[2-]=abcdef</c>
/// <c>cdef</c>, <c>a[2-9]=abcdef</c> <c>cdef</c>, <c>a[9-10]=abcdef</c> nothing, <c>a[0-0]=abcdef</c>
/// <c>a</c>, <c>a[01-02]=abcd</c> <c>bc</c>; <c>a[2]=</c>, <c>a[3-1]=</c>, <c>a[1-x]=</c> and
/// <c>a[1-99999999999999999999]=</c> exit 2 with <c>curl: option --variable: syntax error in --variable
/// argument</c>; <c>a</c>, <c>a b=1</c>, <c>a[x]=abcdef</c> and <c>a[1-2]x=abcd</c> warn
/// <c>Warning: Bad --variable syntax, skipping: &lt;value&gt;</c>; <c>=1</c>, <c>%</c> and the empty value warn
/// <c>Warning: Bad variable name length (0), skipping</c>, and a 128-letter name <c>(128)</c>, while 127
/// letters are accepted; <c>a@missing.txt</c> exits 26 with <c>curl: Failed to open missing.txt: No such
/// file or directory</c>, <c>a@.</c> with <c>Permission denied</c> and <c>a@</c> with <c>Invalid argument</c>;
/// <c>%MYV</c> imports <c>envval</c>, <c>%NOPE</c> exits 2 with <c>curl: Variable 'NOPE' import fail, not
/// set</c> and <c>curl: option --variable: variable expansion failure</c>, <c>%NOPE=def</c> gives <c>def</c>,
/// <c>%MYV=def</c>, <c>%MYV@missing.txt</c>, <c>%MYV-</c> and <c>%MYV[1-2]=zzzz</c> give <c>envval</c>,
/// <c>%NOPE[1-2]=zzzz</c> gives <c>zz</c> and <c>%NOPE-</c> warns bad syntax; <c>a=1 a=2</c> gives <c>2</c>,
/// with <c>Note: Overwriting variable 'a'</c> when <c>-v</c> or <c>--trace-ascii</c> came first, even with
/// <c>-s</c>; <c>-s</c> hides the warnings, and hides the error line unless <c>-S</c> follows it.
/// </summary>
[TestClass]
public sealed class CommandLineVariableOptionTests
{
    private const string Url = "http://example.com/";

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    [DataRow("a[1-3]=abcdef", "bcd")]
    [DataRow("a[2-]=abcdef", "cdef")]
    [DataRow("a[2-9]=abcdef", "cdef")]
    [DataRow("a[9-10]=abcdef", "")]
    [DataRow("a[0-0]=abcdef", "a")]
    [DataRow("a[01-02]=abcd", "bc")]
    [DataRow("a[1-2]=", "")]
    [DataRow("a=hello", "hello")]
    [DataRow("a=", "")]
    [DataRow("a=x=y", "x=y")]
    public void Parse_VariableWithContent_SetsTheBytesInItsRange(string definition, string expected)
    {
        CommandLineParseResult result = Parse(["--variable", definition, "--expand-user-agent", "[{{a}}]", Url]);

        Diagnostics.Assert("accepted", true, result.IsAccepted);
        AssertUserAgent($"[{expected}]", result);
        AssertWarnings(result);
        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual($"[{expected}]", result.Options!.UserAgent);
        Assert.IsEmpty(result.WarningLines);
    }

    [TestMethod]
    public void Parse_VariableNameWithDigitsAndUnderscores_IsAccepted()
    {
        CommandLineParseResult result = Parse(["--variable", "a_Z9=v", "--expand-user-agent", "{{a_Z9}}", Url]);

        AssertUserAgent("v", result);
        Assert.AreEqual("v", result.Options!.UserAgent);
    }

    [TestMethod]
    public void Parse_VariableGivenTwice_KeepsTheLastWithoutANoteUnlessTracing()
    {
        CommandLineParseResult result = Parse(["--variable", "a=1", "--variable", "a=2", "--expand-user-agent", "{{a}}", Url]);

        AssertUserAgent("2", result);
        AssertWarnings(result);
        Assert.AreEqual("2", result.Options!.UserAgent);
        Assert.IsEmpty(result.WarningLines);
    }

    [TestMethod]
    [DataRow("-v")]
    [DataRow("--trace-ascii")]
    public void Parse_VariableGivenTwiceWhileTracing_AddsCurlsOverwritingNoteEvenWhenSilent(string trace)
    {
        string[] traceArguments = trace == "-v" ? ["-v"] : [trace, "trace.txt"];

        CommandLineParseResult result = Parse(["-s", .. traceArguments, "--variable", "a=1", "--variable", "a=2", Url]);

        Diagnostics.Assert("accepted", true, result.IsAccepted);
        AssertWarnings(result, "Note: Overwriting variable 'a'");
        Assert.IsTrue(result.IsAccepted);
        CollectionAssert.AreEqual(new[] { "Note: Overwriting variable 'a'" }, result.WarningLines.ToArray());
    }

    [TestMethod]
    public void Parse_VariableSetOnceWhileTracing_AddsNoNote()
    {
        CommandLineParseResult result = Parse(["-v", "--variable", "a=1", Url]);

        AssertWarnings(result);
        Assert.IsEmpty(result.WarningLines);
    }

    [TestMethod]
    [DataRow("a[2]=abcdef")]
    [DataRow("a[3-1]=abcdef")]
    [DataRow("a[1-x]=abcdef")]
    [DataRow("a[1-2x=abcdef")]
    [DataRow("a[1")]
    [DataRow("a[1-99999999999999999999]=abcd")]
    [DataRow("a[99999999999999999999-1]=abcd")]
    public void Parse_VariableWithMalformedRange_IsRefusedAsASyntaxError(string definition)
    {
        CommandLineParseResult result = Parse(["--variable", definition, Url]);

        Diagnostics.AssertRefusal(
            result,
            CurlExitCode.FailedInit,
            ["curl: option --variable: syntax error in --variable argument", CommandLineRefusal.TryHelpLine]);
        Assert.IsFalse(result.IsAccepted);
        Assert.AreEqual(CurlExitCode.FailedInit, result.Refusal!.ExitCode);
        CollectionAssert.AreEqual(
            new[] { "curl: option --variable: syntax error in --variable argument", CommandLineRefusal.TryHelpLine },
            result.Refusal.StandardErrorLines.ToArray());
    }

    [TestMethod]
    public void Parse_VariableRangeUpToTheLargestOffset_IsAccepted()
    {
        CommandLineParseResult result = Parse(["--variable", "a[1-9223372036854775807]=abc", "--expand-user-agent", "{{a}}", Url]);

        AssertUserAgent("bc", result);
        Assert.AreEqual("bc", result.Options!.UserAgent);
    }

    [TestMethod]
    [DataRow("a")]
    [DataRow("a b=1")]
    [DataRow("a[x]=abcdef")]
    [DataRow("a[1-2]x=abcd")]
    [DataRow("a[1-2]")]
    [DataRow("a[")]
    public void Parse_VariableWithoutEqualsOrAt_IsSkippedWithCurlsWarning(string definition)
    {
        CommandLineParseResult result = Parse(["--variable", definition, "--expand-user-agent", "[{{a}}]", Url]);

        Diagnostics.Assert("accepted", true, result.IsAccepted);
        AssertUserAgent("[]", result);
        AssertWarnings(result, $"Warning: Bad --variable syntax, skipping: {definition}");
        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual("[]", result.Options!.UserAgent);
        CollectionAssert.AreEqual(new[] { $"Warning: Bad --variable syntax, skipping: {definition}" }, result.WarningLines.ToArray());
    }

    [TestMethod]
    [DataRow("=1", 0)]
    [DataRow("%", 0)]
    [DataRow("", 0)]
    [DataRow("-a=1", 0)]
    [DataRow("%%a=1", 0)]
    public void Parse_VariableWithEmptyName_IsSkippedWithCurlsWarning(string definition, int length)
    {
        CommandLineParseResult result = Parse(["--variable", definition, Url]);

        Diagnostics.Assert("accepted", true, result.IsAccepted);
        AssertWarnings(result, $"Warning: Bad variable name length ({length}), skipping");
        Assert.IsTrue(result.IsAccepted);
        CollectionAssert.AreEqual(new[] { $"Warning: Bad variable name length ({length}), skipping" }, result.WarningLines.ToArray());
    }

    [TestMethod]
    public void Parse_VariableNameOf128Letters_IsSkippedAnd127AreAccepted()
    {
        string longName = new('x', 128);
        string longestName = new('x', 127);

        CommandLineParseResult result = Parse(["--variable", $"{longName}=1", "--variable", $"{longestName}=2", "--expand-user-agent", $"{{{{{longestName}}}}}", Url]);

        AssertUserAgent("2", result);
        AssertWarnings(result, "Warning: Bad variable name length (128), skipping");
        Assert.AreEqual("2", result.Options!.UserAgent);
        CollectionAssert.AreEqual(new[] { "Warning: Bad variable name length (128), skipping" }, result.WarningLines.ToArray());
    }

    [TestMethod]
    public void Parse_VariableWarningAfterSilent_IsHidden()
    {
        CommandLineParseResult result = Parse(["-s", "--variable", "a", "--variable", "=1", Url]);

        Diagnostics.Assert("accepted", true, result.IsAccepted);
        AssertWarnings(result);
        Assert.IsTrue(result.IsAccepted);
        Assert.IsEmpty(result.WarningLines);
    }

    [TestMethod]
    public void Parse_VariableAtFile_ReadsTheFileInItsRange()
    {
        RecordingDataFileReader reader = new() { Files = { ["vf.txt"] = "file\r\ncontent\n"u8.ToArray() } };
        Diagnostics.Bytes("vf.txt", reader.Files["vf.txt"]);

        CommandLineParseResult result = Parse(["--variable", "a[1-2]@vf.txt", "--variable", "b[20-]@vf.txt", "--variable", "c@vf.txt", "--expand-data", "{{a}}|{{b}}|{{c}}", Url], reader);

        Diagnostics.Assert("accepted", true, result.IsAccepted);
        AssertPostData("il||file\r\ncontent\n"u8, result);
        AssertReads(reader, "vf.txt", "vf.txt", "vf.txt");
        Assert.IsTrue(result.IsAccepted);
        CollectionAssert.AreEqual("il||file\r\ncontent\n"u8.ToArray(), result.Options!.PostData!.Value.ToArray());
        CollectionAssert.AreEqual(new[] { "vf.txt", "vf.txt", "vf.txt" }, reader.Reads);
    }

    [TestMethod]
    public void Parse_VariableAtDash_ReadsStandardInputInItsRange()
    {
        RecordingDataFileReader reader = new() { StandardInput = "stdin\n"u8.ToArray() };
        Diagnostics.Bytes("standard input", reader.StandardInput);

        CommandLineParseResult result = Parse(["--variable", "a[1-]@-", "--expand-user-agent", "{{a}}", Url], reader);

        AssertUserAgent("tdin\n", result);
        AssertReads(reader, "-");
        Assert.AreEqual("tdin\n", result.Options!.UserAgent);
        CollectionAssert.AreEqual(new[] { "-" }, reader.Reads);
    }

    [TestMethod]
    [DataRow("missing.txt", false, "No such file or directory")]
    [DataRow(".", true, "Permission denied")]
    [DataRow("", true, "Invalid argument")]
    public void Parse_VariableAtUnreadableFile_IsRefusedWithReadErrorAndTheReason(string file, bool exists, string reason)
    {
        Diagnostics.Arrange("path exists", exists);
        CommandLineParseResult result = Parse(["--variable", $"a@{file}", Url], new RecordingDataFileReader(), exists);

        Diagnostics.AssertRefusal(
            result,
            CurlExitCode.ReadError,
            [$"curl: Failed to open {file}: {reason}", "curl: option --variable: error encountered when reading a file", CommandLineRefusal.TryHelpLine]);
        Assert.AreEqual(CurlExitCode.ReadError, result.Refusal!.ExitCode);
        CollectionAssert.AreEqual(
            new[]
            {
                $"curl: Failed to open {file}: {reason}",
                "curl: option --variable: error encountered when reading a file",
                CommandLineRefusal.TryHelpLine,
            },
            result.Refusal.StandardErrorLines.ToArray());
    }

    [TestMethod]
    public void Parse_VariableAtUnreadableFileAfterSilent_HidesTheErrorLine()
    {
        CommandLineParseResult result = Parse(["-s", "--variable", "a@missing.txt", Url]);

        AssertStandardError(result, "curl: option --variable: error encountered when reading a file", CommandLineRefusal.TryHelpLine);
        CollectionAssert.AreEqual(
            new[] { "curl: option --variable: error encountered when reading a file", CommandLineRefusal.TryHelpLine },
            result.Refusal!.StandardErrorLines.ToArray());
    }

    [TestMethod]
    public void Parse_VariableImportingASetEnvironmentVariable_TakesItsValueOverAnyDefaultOrRange()
    {
        using EnvironmentVariable set = new("envval");
        string name = set.Name;
        Diagnostics.Arrange("environment variable", $"{name}=envval");

        CommandLineParseResult result = Parse(
            [
                "--variable", $"%{name}",
                "--expand-user-agent", $"{{{{{name}}}}}",
                "--variable", $"%{name}=def", "--expand-data", $"{{{{{name}}}}}",
                "--variable", $"%{name}@missing.txt", "--expand-data", $"{{{{{name}}}}}",
                "--variable", $"%{name}-", "--expand-data", $"{{{{{name}}}}}",
                "--variable", $"%{name}[1-2]=zzzz", "--expand-data", $"{{{{{name}}}}}",
                Url,
            ]);

        Diagnostics.Assert("accepted", true, result.IsAccepted);
        AssertUserAgent("envval", result);
        AssertPostData("envval&envval&envval&envval"u8, result);
        AssertWarnings(result);
        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual("envval", result.Options!.UserAgent);
        CollectionAssert.AreEqual("envval&envval&envval&envval"u8.ToArray(), result.Options!.PostData!.Value.ToArray());
        Assert.IsEmpty(result.WarningLines);
    }

    [TestMethod]
    public void Parse_GivenAnEnvironmentReader_ImportsTheVariableThroughItAndNotTheProcessEnvironment()
    {
        string[] arguments = ["--variable", "%FUNVALUE", "--expand-data", "{{FUNVALUE}}", Url];
        Diagnostics.ArrangeArguments(arguments);
        Diagnostics.Arrange("reader", "FUNVALUE=contents; process FUNVALUE " + Quoted(Environment.GetEnvironmentVariable("FUNVALUE")));
        Assert.IsNull(Environment.GetEnvironmentVariable("FUNVALUE"), "The process environment must not hold FUNVALUE for this test to mean anything.");

        CommandLineParseResult result = CommandLineParser.Parse(
            arguments,
            _ => false,
            new UnexpectedPasswordPrompt(),
            new RecordingDataFileReader(),
            new DefaultConfigFileSearch(_ => null, false, null, null),
            false,
            name => name == "FUNVALUE" ? "contents" : null);

        Diagnostics.ActParse(result);
        AssertPostData("contents"u8, result);
        Assert.IsTrue(result.IsAccepted);
        CollectionAssert.AreEqual("contents"u8.ToArray(), result.Options!.PostData!.Value.ToArray());
    }

    [TestMethod]
    public void Parse_GivenANullEnvironmentReader_ThrowsArgumentNullException() =>
        Assert.ThrowsExactly<ArgumentNullException>(() => CommandLineParser.Parse(
            [Url],
            _ => false,
            new UnexpectedPasswordPrompt(),
            new RecordingDataFileReader(),
            new DefaultConfigFileSearch(_ => null, false, null, null),
            false,
            null!));

    [TestMethod]
    public void Parse_VariableImportingAnUnsetEnvironmentVariableWithADefault_TakesTheDefaultInItsRange()
    {
        string name = EnvironmentVariable.UnsetName();
        RecordingDataFileReader reader = new() { Files = { ["d.txt"] = "file"u8.ToArray() } };
        Diagnostics.Arrange("unset environment variable", name);
        Diagnostics.Bytes("d.txt", reader.Files["d.txt"]);

        CommandLineParseResult result = Parse(["--variable", $"%{name}[1-2]=zzzz", "--expand-user-agent", $"{{{{{name}}}}}", "--variable", $"%{name}@d.txt", "--expand-data", $"{{{{{name}}}}}", Url], reader);

        AssertUserAgent("zz", result);
        AssertPostData("file"u8, result);
        Assert.AreEqual("zz", result.Options!.UserAgent);
        CollectionAssert.AreEqual("file"u8.ToArray(), result.Options!.PostData!.Value.ToArray());
    }

    [TestMethod]
    public void Parse_VariableImportingAnUnsetEnvironmentVariableWithBadSyntax_IsSkippedWithCurlsWarning()
    {
        string name = EnvironmentVariable.UnsetName();
        Diagnostics.Arrange("unset environment variable", name);

        CommandLineParseResult result = Parse(["--variable", $"%{name}-", Url]);

        Diagnostics.Assert("accepted", true, result.IsAccepted);
        AssertWarnings(result, $"Warning: Bad --variable syntax, skipping: %{name}-");
        Assert.IsTrue(result.IsAccepted);
        CollectionAssert.AreEqual(new[] { $"Warning: Bad --variable syntax, skipping: %{name}-" }, result.WarningLines.ToArray());
    }

    [TestMethod]
    public void Parse_VariableImportingASetEnvironmentVariableWithAMalformedRange_IsRefusedAsASyntaxError()
    {
        using EnvironmentVariable set = new("envval");
        Diagnostics.Arrange("environment variable", $"{set.Name}=envval");

        CommandLineParseResult result = Parse(["--variable", $"%{set.Name}[1-x]", Url]);

        Diagnostics.Assert(
            "first stderr line",
            "curl: option --variable: syntax error in --variable argument",
            CommandLineParseDiagnostics.Peek(result.Refusal)?.StandardErrorLines.FirstOrDefault());
        Assert.AreEqual("curl: option --variable: syntax error in --variable argument", result.Refusal!.StandardErrorLines[0]);
    }

    [TestMethod]
    public void Parse_VariableImportingAnUnsetEnvironmentVariableWithoutDefault_IsRefusedAsAnExpansionFailure()
    {
        string name = EnvironmentVariable.UnsetName();
        Diagnostics.Arrange("unset environment variable", name);

        CommandLineParseResult result = Parse(["--variable", $"%{name}", Url]);

        Diagnostics.AssertRefusal(
            result,
            CurlExitCode.FailedInit,
            [$"curl: Variable '{name}' import fail, not set", "curl: option --variable: variable expansion failure", CommandLineRefusal.TryHelpLine]);
        Assert.AreEqual(CurlExitCode.FailedInit, result.Refusal!.ExitCode);
        CollectionAssert.AreEqual(
            new[]
            {
                $"curl: Variable '{name}' import fail, not set",
                "curl: option --variable: variable expansion failure",
                CommandLineRefusal.TryHelpLine,
            },
            result.Refusal.StandardErrorLines.ToArray());
    }

    [TestMethod]
    public void Parse_VariableImportFailureAfterSilent_HidesTheErrorLineUnlessShowError()
    {
        string name = EnvironmentVariable.UnsetName();
        Diagnostics.Arrange("unset environment variable", name);

        CommandLineParseResult silent = Parse(["-s", "--variable", $"%{name}", Url]);
        CommandLineParseResult shown = Parse(["-s", "-S", "--variable", $"%{name}", Url]);

        Diagnostics.Assert("silent stderr line count", 2, CommandLineParseDiagnostics.Peek(silent.Refusal)?.StandardErrorLines.Count);
        Diagnostics.Assert("shown stderr line count", 3, CommandLineParseDiagnostics.Peek(shown.Refusal)?.StandardErrorLines.Count);
        Assert.HasCount(2, silent.Refusal!.StandardErrorLines);
        Assert.HasCount(3, shown.Refusal!.StandardErrorLines);
    }

    [TestMethod]
    public void Parse_VariableInAConfigFile_IsSetAsOnTheCommandLine()
    {
        RecordingDataFileReader reader = new() { Files = { ["k.cfg"] = "variable = a=hi\nexpand-data = \"[{{a:b64}}]\"\n"u8.ToArray() } };
        Diagnostics.Bytes("k.cfg", reader.Files["k.cfg"]);

        CommandLineParseResult result = Parse(["-K", "k.cfg", Url], reader);

        Diagnostics.Assert("accepted", true, result.IsAccepted);
        AssertPostData("[aGk=]"u8, result);
        Assert.IsTrue(result.IsAccepted);
        CollectionAssert.AreEqual("[aGk=]"u8.ToArray(), result.Options!.PostData!.Value.ToArray());
    }

    private CommandLineParseResult Parse(IReadOnlyList<string> arguments) =>
        Parse(arguments, new RecordingDataFileReader());

    /// <summary>Parses <paramref name="arguments"/>, writing them, the outcome, the user agent and the post data as diagnostics.</summary>
    private CommandLineParseResult Parse(IReadOnlyList<string> arguments, IDataFileReader reader, bool pathExists = false)
    {
        Diagnostics.ArrangeArguments(arguments);
        CommandLineParseResult result = CommandLineParser.Parse(arguments, _ => pathExists, new UnexpectedPasswordPrompt(), reader);
        Diagnostics.ActParse(result);
        if (result.IsAccepted)
        {
            Diagnostics.Act("user agent", Quoted(result.Options.UserAgent));
            if (result.Options.PostData is { } postData)
            {
                Diagnostics.Bytes("post data", postData.Span);
            }
        }

        return result;
    }

    private static string Quoted(string? value) => value is null ? "null" : "\"" + value + "\"";

    private void AssertUserAgent(string expected, CommandLineParseResult result) =>
        Diagnostics.Assert("user agent", Quoted(expected), Quoted(result.Options?.UserAgent));

    private void AssertWarnings(CommandLineParseResult result, params string[] expected) =>
        Diagnostics.Assert("warning lines", CommandLineParseDiagnostics.QuoteEach(expected), CommandLineParseDiagnostics.QuoteEach(result.WarningLines));

    private void AssertStandardError(CommandLineParseResult result, params string[] expected) =>
        Diagnostics.Assert("stderr", CommandLineParseDiagnostics.QuoteEach(expected), CommandLineParseDiagnostics.QuoteEach(CommandLineParseDiagnostics.Peek(result.Refusal)?.StandardErrorLines ?? []));

    private void AssertReads(RecordingDataFileReader reader, params string[] expected) =>
        Diagnostics.Assert("reads", CommandLineParseDiagnostics.QuoteEach(expected), CommandLineParseDiagnostics.QuoteEach(reader.Reads));

    private void AssertPostData(ReadOnlySpan<byte> expected, CommandLineParseResult result)
    {
        ReadOnlyMemory<byte>? actual = result.Options?.PostData;
        if (actual is { } bytes)
        {
            Diagnostics.Diff("post data", expected, bytes.Span);
        }
        else
        {
            Diagnostics.Assert("post data", "bytes", null);
        }
    }

    private sealed class UnexpectedPasswordPrompt : IPasswordPrompt
    {
        public string ReadPassword(string prompt) => throw new AssertFailedException("No password prompt was expected.");
    }

    /// <summary>An environment variable with a name no other test uses, set for the length of a test.</summary>
    private sealed class EnvironmentVariable : IDisposable
    {
        public EnvironmentVariable(string value)
        {
            Name = UnsetName();
            Environment.SetEnvironmentVariable(Name, value);
        }

        public string Name { get; }

        public static string UnsetName() => "CT" + Guid.NewGuid().ToString("N")[..16];

        public void Dispose() => Environment.SetEnvironmentVariable(Name, null);
    }
}
