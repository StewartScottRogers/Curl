using Curl.Protocol.Abstractions;

namespace Curl.Cli;

[TestClass]
public sealed class CommandLineExtensionOptionTests
{
    private const string Url = "http://example.com/";

    private static readonly CommandLineExtensionOption CacheDirectory = new("cache-dir", TakesValue: true);
    private static readonly CommandLineExtensionOption Sse = new("sse", TakesValue: false);

    private static CommandLineParseResult Parse(IReadOnlyList<string> arguments, params CommandLineExtensionOption[] extensionOptions) =>
        CommandLineParser.Parse(arguments, _ => true, new EmptyPasswordPrompt(), new RecordingDataFileReader(), new DefaultConfigFileSearch(_ => null, false, null, null), isWindows: false, _ => null, actsAsQuicBuild: false, actsAsEchBuild: false, actsAsCAresBuild: false, extensionOptions);

    [TestMethod]
    public void Parse_AnExtensionOptionWithItsValueNext_RecordsTheValue()
    {
        CommandLineParseResult result = Parse(["--cache-dir", "/tmp/cache", Url], CacheDirectory, Sse);

        Assert.IsNull(result.Refusal);
        Assert.AreEqual("/tmp/cache", result.Options!.ExtensionOptionValues["cache-dir"]);
        Assert.IsFalse(result.Options!.ExtensionOptionValues.ContainsKey("sse"));
        CollectionAssert.AreEqual(new[] { Url }, result.Options!.Urls.ToArray());
    }

    [TestMethod]
    public void Parse_AnExtensionOptionWithItsValueAttached_RecordsTheLastValue()
    {
        CommandLineParseResult result = Parse(["--cache-dir=/a", "--cache-dir=/b", Url], CacheDirectory);

        Assert.IsNull(result.Refusal);
        Assert.AreEqual("/b", result.Options!.ExtensionOptionValues["cache-dir"]);
    }

    [TestMethod]
    public void Parse_AnExtensionFlag_RecordsTheEmptyString()
    {
        CommandLineParseResult result = Parse(["--sse", Url], Sse);

        Assert.IsNull(result.Refusal);
        Assert.AreEqual(string.Empty, result.Options!.ExtensionOptionValues["sse"]);
    }

    [TestMethod]
    public void Parse_AnExtensionOptionWithNoValue_RefusesAsCurlRefusesAMissingValue()
    {
        CommandLineParseResult withExtension = Parse(["--cache-dir"], CacheDirectory);
        CommandLineParseResult curlOption = Parse(["--output"]);

        Assert.IsNotNull(withExtension.Refusal);
        Assert.IsNotNull(curlOption.Refusal);
        Assert.AreEqual(CurlExitCode.FailedInit, withExtension.Refusal.ExitCode);
        Assert.AreEqual(curlOption.Refusal.StandardErrorLines[0].Replace("--output", "--cache-dir", StringComparison.Ordinal), withExtension.Refusal.StandardErrorLines[0]);
    }

    [TestMethod]
    public void Parse_AnExtensionOptionWithExtensionsOff_RefusesItAsAnUnknownOption()
    {
        CommandLineParseResult result = Parse(["--cache-dir", "/tmp/cache", Url]);
        CommandLineParseResult unknown = Parse(["--bogus", "/tmp/cache", Url]);

        Assert.IsNotNull(result.Refusal);
        Assert.IsNotNull(unknown.Refusal);
        Assert.AreEqual(2, (int)result.Refusal.ExitCode);
        Assert.AreEqual("curl: option --cache-dir: is unknown", result.Refusal.StandardErrorLines[0]);
        CollectionAssert.AreEqual(unknown.Refusal.StandardErrorLines.Skip(1).ToArray(), result.Refusal.StandardErrorLines.Skip(1).ToArray());
    }

    [TestMethod]
    public void Parse_AnExtensionOptionOfACurlName_ReadsCurlsOption()
    {
        CommandLineParseResult result = Parse(["--output", "file", Url], new CommandLineExtensionOption("output", TakesValue: true));

        Assert.IsNull(result.Refusal);
        Assert.IsEmpty(result.Options!.ExtensionOptionValues);
    }

    [TestMethod]
    public void Parse_WithNoExtensionOptionList_Throws() =>
        Assert.ThrowsExactly<ArgumentNullException>(() => Parse([Url], null!));

    [TestMethod]
    public void Parse_WithNoConfigFileSearch_Throws() =>
        Assert.ThrowsExactly<ArgumentNullException>(() => CommandLineParser.Parse([Url], _ => true, new EmptyPasswordPrompt(), new RecordingDataFileReader(), null!, isWindows: false, _ => null, actsAsQuicBuild: false, actsAsEchBuild: false, actsAsCAresBuild: false, []));

    [TestMethod]
    public void IsCurlLongOption_CurlsOwnName_IsTrue() =>
        Assert.IsTrue(CommandLineParser.IsCurlLongOption("output"));

    [TestMethod]
    public void IsCurlLongOption_AnotherName_IsFalse() =>
        Assert.IsFalse(CommandLineParser.IsCurlLongOption("cache-dir"));

    [TestMethod]
    public void IsCurlLongOption_WithNoName_Throws() =>
        Assert.ThrowsExactly<ArgumentNullException>(() => CommandLineParser.IsCurlLongOption(null!));
}
