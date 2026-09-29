using Curl.Cli;

namespace Curl.Console;

/// <summary>
/// Pins <see cref="TlsBuildInformation.Lines" />: no build-time engines for <c>--engine list</c>, nothing for
/// <c>--dump-ca-embed</c>, and no answer for a command line that asks neither (ADR-0151).
/// </summary>
[TestClass]
public sealed class TlsBuildInformationTests
{
    [TestMethod]
    public void Lines_EngineList_ListsNoEngines()
    {
        CollectionAssert.AreEqual(
            new[] { "Build-time engines:", "  <none>" },
            TlsBuildInformation.Lines(Parse("--engine", "list"))!.ToArray());
    }

    [TestMethod]
    public void Lines_DumpCaEmbed_IsNoLines()
    {
        Assert.IsEmpty(TlsBuildInformation.Lines(Parse("--dump-ca-embed"))!);
    }

    [TestMethod]
    public void Lines_NeitherOption_IsNull()
    {
        Assert.IsNull(TlsBuildInformation.Lines(Parse("--engine", "pkcs11", "https://example.com/")));
    }

    private static CommandLineOptions Parse(params string[] arguments)
    {
        CommandLineParseResult parsed = CommandLineParser.Parse(arguments, _ => true);
        Assert.IsTrue(parsed.IsAccepted);
        return parsed.Options;
    }
}
