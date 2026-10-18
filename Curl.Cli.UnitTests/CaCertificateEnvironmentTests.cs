using Curl.Testing;

namespace Curl.Cli;

/// <summary>
/// Pins <see cref="CaCertificateEnvironment"/> through the parser (BL-2723, ADR-0682): with no
/// <c>--cacert</c> or <c>--capath</c>, curl 8.22.0's OpenSSL build takes the CA file from
/// <c>CURL_CA_BUNDLE</c>, or else the CA folder from <c>SSL_CERT_DIR</c> and the CA file from
/// <c>SSL_CERT_FILE</c>; the command line wins, <c>-k</c> skips the lookup unless a checked
/// <c>--doh-url</c> needs it, and the Schannel build reads none of them.
/// </summary>
[TestClass]
public sealed class CaCertificateEnvironmentTests
{
    private const string Url = "https://127.0.0.1:1/";

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    [DataRow("ca.pem", null, null, "ca.pem", null)]
    [DataRow("ca.pem", "certs", "file.pem", "ca.pem", null)]
    [DataRow(null, "certs", null, null, "certs")]
    [DataRow(null, null, "file.pem", "file.pem", null)]
    [DataRow(null, "certs", "file.pem", "file.pem", "certs")]
    [DataRow(null, null, null, null, null)]
    public void Parse_NoCaOptionsOffSchannel_TakesTheCaLocationsFromTheEnvironmentInCurlsOrder(string? bundle, string? directory, string? file, string? expectedFile, string? expectedDirectory)
    {
        Func<string, string?> environment = Environment(bundle, directory, file);

        CommandLineOptions options = Accept([Url], environment, isWindows: false);

        AssertCaLocations(expectedFile, expectedDirectory, options);
    }

    [TestMethod]
    [DataRow("--cacert", "given.pem", "given.pem", null)]
    [DataRow("--capath", "given", null, "given")]
    public void Parse_CaOptionGiven_WinsOverTheEnvironment(string option, string value, string? expectedFile, string? expectedDirectory)
    {
        Func<string, string?> environment = Environment("ca.pem", "certs", "file.pem");

        CommandLineOptions options = Accept([option, value, Url], environment, isWindows: false);

        AssertCaLocations(expectedFile, expectedDirectory, options);
    }

    [TestMethod]
    public void Parse_SchannelBuild_ReadsNoneOfTheVariables()
    {
        List<string> asked = [];

        CommandLineOptions options = Accept([Url], name => { asked.Add(name); return "set"; }, isWindows: true);

        AssertCaLocations(null, null, options);
        Diagnostics.Assert("variables read", "", string.Join(',', asked.Where(name => name.StartsWith("SSL_CERT", StringComparison.Ordinal) || name == "CURL_CA_BUNDLE")));
        Assert.IsFalse(asked.Any(name => name.StartsWith("SSL_CERT", StringComparison.Ordinal) || name == "CURL_CA_BUNDLE"));
    }

    [TestMethod]
    [DataRow(new[] { "-k" }, null)]
    [DataRow(new[] { "-k", "--doh-url", "https://dns.example/" }, "ca.pem")]
    [DataRow(new[] { "-k", "--doh-url", "https://dns.example/", "--doh-insecure" }, null)]
    [DataRow(new[] { "--doh-insecure" }, "ca.pem")]
    public void Parse_Insecure_SkipsTheEnvironmentUnlessACheckedDohUrlNeedsIt(string[] arguments, string? expectedFile)
    {
        Func<string, string?> environment = Environment("ca.pem", null, null);

        CommandLineOptions options = Accept([.. arguments, Url], environment, isWindows: false);

        AssertCaLocations(expectedFile, null, options);
    }

    [TestMethod]
    public void Parse_NextGroup_TakesTheEnvironmentForEachGroupWithoutItsOwnCaOption()
    {
        Func<string, string?> environment = Environment("ca.pem", null, null);

        CommandLineParseResult result = Parse(["--cacert", "given.pem", Url, "--next", Url], environment, isWindows: false);

        string groups = string.Join(',', result.Groups.Select(group => group.CaCertificateFile));
        Diagnostics.Assert("CA file per group", "given.pem,ca.pem", groups);
        Assert.AreEqual("given.pem,ca.pem", groups);
    }

    private static Func<string, string?> Environment(string? bundle, string? directory, string? file) =>
        name => name switch
        {
            "CURL_CA_BUNDLE" => bundle,
            "SSL_CERT_DIR" => directory,
            "SSL_CERT_FILE" => file,
            _ => null,
        };

    private void AssertCaLocations(string? expectedFile, string? expectedDirectory, CommandLineOptions options)
    {
        Diagnostics.Assert("CA file", expectedFile, options.CaCertificateFile);
        Diagnostics.Assert("CA folder", expectedDirectory, options.CaCertificateDirectory);
        Assert.AreEqual(expectedFile, options.CaCertificateFile);
        Assert.AreEqual(expectedDirectory, options.CaCertificateDirectory);
    }

    private CommandLineOptions Accept(string[] arguments, Func<string, string?> environment, bool isWindows)
    {
        CommandLineParseResult result = Parse(arguments, environment, isWindows);
        Assert.IsTrue(result.IsAccepted);
        return result.Options;
    }

    private CommandLineParseResult Parse(string[] arguments, Func<string, string?> environment, bool isWindows)
    {
        Diagnostics.ArrangeArguments(arguments);

        CommandLineParseResult result = CommandLineParser.Parse(arguments, _ => true, ConsolePasswordPrompt.ForProcessConsole, new RecordingDataFileReader(), new DefaultConfigFileSearch(_ => null, false, null, null), isWindows, environment);
        Diagnostics.ActParse(result);
        return result;
    }
}
