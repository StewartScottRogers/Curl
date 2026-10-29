using Curl.Core;
using Curl.Protocol.Abstractions;

namespace Curl.Cli;

/// <summary>
/// Pins CGR-110 part 2 (BL-3973): with standard input closed (<see cref="ClosedStandardInputStream"/>),
/// curl 8.22.0 refuses <c>-d @-</c> and the other <c>file2memory</c> readers with exit 26 and no
/// <c>Failed to open</c> line, and reads <c>-H @-</c> and <c>--url @-</c> as empty (ADR-0949).
/// </summary>
[TestClass]
public sealed class Cgr110DataOptionTests
{
    private const string Url = "http://127.0.0.1:1/ok";

    [TestMethod]
    [DataRow("-d", "@-")]
    [DataRow("--data-binary", "@-")]
    [DataRow("--json", "@-")]
    [DataRow("--data-urlencode", "@-")]
    [DataRow("--data-urlencode", "x@-")]
    [DataRow("--url-query", "@-")]
    [DataRow("--variable", "x@-")]
    [DataRow("-w", "@-")]
    public void Parse_StandardInputValueWithStandardInputClosed_IsRefusedWithReadError(string option, string value)
    {
        CommandLineParseResult result = Parse([option, value, Url], new ClosedStandardInputStream());

        Assert.AreEqual(CurlExitCode.ReadError, result.Refusal?.ExitCode);
        CollectionAssert.AreEqual(
            new[] { $"curl: option {option}: error encountered when reading a file", CommandLineRefusal.TryHelpLine },
            result.Refusal!.StandardErrorLines.ToArray());
    }

    [TestMethod]
    public void Parse_DataFromStandardInputWithStandardInputEmpty_SendsAnEmptyBody()
    {
        CommandLineParseResult result = Parse(["-d", "@-", Url], Stream.Null);

        Assert.IsNull(result.Refusal);
    }

    [TestMethod]
    [DataRow("-H")]
    [DataRow("--url")]
    public void Parse_FgetsReadStandardInputValueWithStandardInputClosed_ReadsAsEmpty(string option)
    {
        CommandLineParseResult result = Parse([option, "@-", Url], new ClosedStandardInputStream());

        Assert.IsNull(result.Refusal);
        Assert.IsEmpty(result.Options!.Headers);
    }

    [TestMethod]
    public void StandardInputUnreadable_NullSpelledOption_Throws() =>
        Assert.ThrowsExactly<ArgumentNullException>(() => CommandLineRefusal.StandardInputUnreadable(null!));

    private static CommandLineParseResult Parse(IReadOnlyList<string> arguments, Stream standardInput) =>
        CommandLineParser.Parse(arguments, _ => true, new EmptyPasswordPrompt(), DiskDataFileReader.ForPlatform(true).WithStandardInput(() => standardInput), true);
}
