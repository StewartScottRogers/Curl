using System.Text;
using Curl.Protocol.Abstractions;

namespace Curl.Cli;

/// <summary>An option an argument expander consumes works only on the command line, so a <c>-K</c> file naming one is refused (BL-5132, ADR-1040).</summary>
[TestClass]
public sealed class ConfigFileArgumentExpanderOptionTests
{
    private const string Url = "http://example.com/";

    private static readonly CommandLineExtensionOption[] ExtensionOptions =
    [
        new("http-file", TakesValue: true, CommandLineOnly: true),
        new("http-env", TakesValue: true, CommandLineOnly: true),
        new("openapi", TakesValue: true, CommandLineOnly: true),
        new("operation", TakesValue: true, CommandLineOnly: true),
        new("checksum", TakesValue: true),
        new("structured-fields", TakesValue: false),
    ];

    private static CommandLineParseResult Parse(IReadOnlyList<string> arguments, string configText)
    {
        RecordingDataFileReader reader = new();
        reader.Files["k.cfg"] = Encoding.UTF8.GetBytes(configText);
        return CommandLineParser.Parse(arguments, _ => true, new EmptyPasswordPrompt(), reader, new DefaultConfigFileSearch(_ => null, false, null, null), isWindows: false, _ => null, actsAsQuicBuild: false, actsAsEchBuild: false, actsAsCAresBuild: false, ExtensionOptions);
    }

    [TestMethod]
    [DataRow("--http-file hf/a.http\n", "--http-file")]
    [DataRow("http-file = \"hf/a.http\"\n", "http-file")]
    [DataRow("--http-env dev\n", "--http-env")]
    [DataRow("http-env = \"dev\"\n", "http-env")]
    [DataRow("--openapi oa.json\n", "--openapi")]
    [DataRow("openapi = \"oa.json\"\n", "openapi")]
    [DataRow("--operation delPet\n", "--operation")]
    [DataRow("operation = \"delPet\"\n", "operation")]
    public void Parse_AnExpanderOptionInAConfigFile_IsRefusedWithExitCode2NamingIt(string line, string written)
    {
        CommandLineParseResult result = Parse(["-K", "k.cfg", Url], line);

        Assert.IsNotNull(result.Refusal);
        Assert.AreEqual(CurlExitCode.FailedInit, result.Refusal.ExitCode);
        string[] lines = [.. result.Refusal.StandardErrorLines];
        Assert.AreEqual($"curl: k.cfg:1 config file option '{written}' cannot be used in a config file", lines[0]);
        Assert.AreEqual("curl: option -K: cannot be used in a config file", lines[1]);
    }

    [TestMethod]
    public void Parse_OtherExtensionOptionsInAConfigFile_AreStillHonoured()
    {
        CommandLineParseResult result = Parse(["-K", "k.cfg", Url], "checksum = \"sha256:ab\"\n--structured-fields\n");

        Assert.IsNull(result.Refusal);
        Assert.AreEqual("sha256:ab", result.Options!.ExtensionOptionValues["checksum"]);
        Assert.AreEqual(string.Empty, result.Options.ExtensionOptionValues["structured-fields"]);
    }

    [TestMethod]
    public void Parse_AnExpanderOptionOnTheCommandLine_IsReadAsBefore()
    {
        CommandLineParseResult result = Parse(["--http-file", "a.http", Url], string.Empty);

        Assert.IsNull(result.Refusal);
        Assert.AreEqual("a.http", result.Options!.ExtensionOptionValues["http-file"]);
    }
}
