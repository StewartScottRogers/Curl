using Curl.Cli;
using Curl.Testing;

namespace Curl.Console;

/// <summary>
/// Pins <see cref="TlsBuildInformation.Lines" />: no build-time engines for <c>--engine list</c>, nothing for
/// <c>--dump-ca-embed</c>, and no answer for a command line that asks neither (ADR-0151).
/// </summary>
[TestClass]
public sealed class TlsBuildInformationTests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public void Lines_EngineList_ListsNoEngines()
    {
        string[] lines = LinesFor("--engine", "list")!.ToArray();

        Diagnostics.Assert("lines", "Build-time engines: |   <none>", string.Join(" | ", lines));
        CollectionAssert.AreEqual(
            new[] { "Build-time engines:", "  <none>" },
            lines);
    }

    [TestMethod]
    public void Lines_DumpCaEmbed_IsNoLines()
    {
        IReadOnlyList<string> lines = LinesFor("--dump-ca-embed")!;

        Diagnostics.Assert("line count", 0, lines.Count);
        Assert.IsEmpty(lines);
    }

    [TestMethod]
    public void Lines_NeitherOption_IsNull()
    {
        IReadOnlyList<string>? lines = LinesFor("--engine", "pkcs11", "https://example.com/");

        Diagnostics.Assert("lines", "null", lines is null ? "null" : string.Join(" | ", lines));
        Assert.IsNull(lines);
    }

    private IReadOnlyList<string>? LinesFor(params string[] arguments)
    {
        Diagnostics.Arrange("command line", string.Join(' ', arguments));
        IReadOnlyList<string>? lines = TlsBuildInformation.Lines(Parse(arguments));
        Diagnostics.Act("lines", lines is null ? "null" : string.Join(" | ", lines));
        return lines;
    }

    private static CommandLineOptions Parse(params string[] arguments)
    {
        CommandLineParseResult parsed = CommandLineParser.Parse(arguments, _ => true);
        Assert.IsTrue(parsed.IsAccepted);
        return parsed.Options;
    }
}
