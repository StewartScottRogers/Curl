using Curl.Testing;

namespace Curl.Cli;

/// <summary>
/// Pins the parser's production <c>--cacert</c> existence check against real paths on the disk: the test
/// assembly's own file and the runtime's base directory both exist, so the parser records either one.
/// The fast tests in <c>Curl.Cli.UnitTests</c> pin the same check against a fake.
/// </summary>
[TestClass]
public sealed class CommandLineTlsOptionIntegrationTests
{
    private const string Url = "https://example.com/";

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    [TestCategory("Integration")]
    public void Parse_DefaultCheckGivenAnExistingFile_RecordsIt()
    {
        string existingFile = typeof(CommandLineTlsOptionIntegrationTests).Assembly.Location;

        CommandLineParseResult result = Parse(["--cacert", existingFile, Url]);

        Diagnostics.Assert("CA certificate file", existingFile, result.Options?.CaCertificateFile);
        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual(existingFile, result.Options.CaCertificateFile);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void Parse_DefaultCheckGivenAnExistingDirectory_RecordsIt()
    {
        string existingDirectory = AppContext.BaseDirectory;

        CommandLineParseResult result = Parse(["--cacert", existingDirectory, Url]);

        Diagnostics.Assert("CA certificate file", existingDirectory, result.Options?.CaCertificateFile);
        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual(existingDirectory, result.Options.CaCertificateFile);
    }

    /// <summary>
    /// Parses <paramref name="arguments"/> with the production file check, writing the arguments, the outcome
    /// and the recorded CA certificate file as diagnostics.
    /// </summary>
    private CommandLineParseResult Parse(IReadOnlyList<string> arguments)
    {
        Diagnostics.ArrangeArguments(arguments);
        Diagnostics.Arrange("path check", "production");
        CommandLineParseResult result = CommandLineParser.Parse(arguments);
        Diagnostics.ActParse(result);
        if (result.IsAccepted)
        {
            Diagnostics.Act("CA certificate file", result.Options.CaCertificateFile);
        }

        return result;
    }
}
