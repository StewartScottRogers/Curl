using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Cli;

/// <summary>
/// Pins how the parser records the ten TLS options of ADR-0151: <c>--curves</c>, <c>--sigalgs</c>,
/// <c>--tls-earlydata</c>, <c>--ech</c>, <c>--ssl-sessions</c>, <c>--engine</c>, <c>--dump-ca-embed</c>,
/// <c>--tlsuser</c>, <c>--tlspassword</c> and <c>--tlsauthtype</c>, with curl 8.21.0's value checks
/// (<c>src/tool_getparam.c</c>, tag <c>curl-8_21_0</c>): blank values refused where curl denies them,
/// <c>--tlsauthtype</c> refused for anything but <c>SRP</c> as the OpenSSL build measured, and
/// <c>--engine list</c> and <c>--dump-ca-embed</c> ending the command line as <c>-V</c> does.
/// </summary>
[TestClass]
public sealed class CommandLineTlsHandshakeOptionTests
{
    private const string Url = "https://127.0.0.1:1/";

    private const string TryHelp = "curl: try 'curl --help' or 'curl --manual' for more information";

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public void Parse_NoneOfTheOptions_LeavesThemNotGiven()
    {
        CommandLineOptions options = Parse([Url]).Options!;

        Diagnostics.Assert("curves", null, options.Curves);
        Diagnostics.Assert("TLS early data", false, options.TlsEarlyData);
        Diagnostics.Assert("TLS auth type", null, options.TlsAuthType);
        Assert.IsNull(options.Curves);
        Assert.IsNull(options.SignatureAlgorithms);
        Assert.IsFalse(options.TlsEarlyData);
        Assert.IsNull(options.Ech);
        Assert.IsNull(options.EchPublicName);
        Assert.IsNull(options.EchConfigList);
        Assert.IsNull(options.SslSessionsFile);
        Assert.IsNull(options.Engine);
        Assert.IsFalse(options.EngineListRequested);
        Assert.IsFalse(options.CaEmbedDumpRequested);
        Assert.IsNull(options.TlsUser);
        Assert.IsNull(options.TlsPassword);
        Assert.IsNull(options.TlsAuthType);
    }

    [TestMethod]
    public void Parse_EveryValueOption_RecordsTheLastValueVerbatim()
    {
        CommandLineParseResult result = Parse(
        [
            "--curves", "P-256", "--curves", "X25519:P-384",
            "--sigalgs", "x", "--sigalgs", "rsa_pss_rsae_sha256:ECDSA+SHA256",
            "--ssl-sessions", "old.bin", "--ssl-sessions", "sess.bin",
            "--engine", "first", "--engine", "pkcs11",
            "--tlsuser", "u1", "--tlsuser", "user",
            "--tlspassword", "p1", "--tlspassword", "secret",
            "--tlsauthtype", "SRP",
            Url,
        ]);

        CommandLineOptions? recorded = CommandLineParseDiagnostics.Peek(result.Options);
        Diagnostics.Assert("curves", "X25519:P-384", recorded?.Curves);
        Diagnostics.Assert("signature algorithms", "rsa_pss_rsae_sha256:ECDSA+SHA256", recorded?.SignatureAlgorithms);
        Diagnostics.Assert("SSL sessions file", "sess.bin", recorded?.SslSessionsFile);
        Diagnostics.Assert("engine", "pkcs11", recorded?.Engine);
        Diagnostics.Assert("TLS user", "user", recorded?.TlsUser);
        Diagnostics.Assert("TLS password", "secret", recorded?.TlsPassword);
        Diagnostics.Assert("TLS auth type", "SRP", recorded?.TlsAuthType);
        Assert.IsTrue(result.IsAccepted);
        CommandLineOptions options = result.Options;
        Assert.AreEqual("X25519:P-384", options.Curves);
        Assert.AreEqual("rsa_pss_rsae_sha256:ECDSA+SHA256", options.SignatureAlgorithms);
        Assert.AreEqual("sess.bin", options.SslSessionsFile);
        Assert.AreEqual("pkcs11", options.Engine);
        Assert.IsFalse(options.EngineListRequested);
        Assert.AreEqual("user", options.TlsUser);
        Assert.AreEqual("secret", options.TlsPassword);
        Assert.AreEqual("SRP", options.TlsAuthType);
    }

    [TestMethod]
    [DataRow("--curves")]
    [DataRow("--sigalgs")]
    [DataRow("--ech")]
    [DataRow("--ssl-sessions")]
    [DataRow("--engine")]
    [DataRow("--tlsuser")]
    [DataRow("--tlsauthtype")]
    public void Parse_EmptyValue_RefusesAsBlank(string spelledOption)
    {
        CommandLineParseResult result = Parse([spelledOption, string.Empty, Url]);

        Diagnostics.AssertRefusal(result, CurlExitCode.FailedInit, [$"curl: option {spelledOption}: blank argument where content is expected", TryHelp]);
        Assert.AreEqual(CurlExitCode.FailedInit, result.Refusal!.ExitCode);
        CollectionAssert.AreEqual(
            new[] { $"curl: option {spelledOption}: blank argument where content is expected", TryHelp },
            result.Refusal.StandardErrorLines.ToArray());
    }

    [TestMethod]
    public void Parse_EmptyTlsPassword_IsAccepted()
    {
        CommandLineParseResult result = Parse(["--tlspassword", string.Empty, Url]);

        Diagnostics.Assert("TLS password", "\"\"", "\"" + CommandLineParseDiagnostics.Peek(result.Options)?.TlsPassword + "\"");
        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual(string.Empty, result.Options.TlsPassword);
    }

    [TestMethod]
    [DataRow("bogus")]
    [DataRow("srp")]
    [DataRow("SRP ")]
    public void Parse_TlsAuthTypeOtherThanSrp_RefusesAsUnsupported(string value)
    {
        CommandLineParseResult result = Parse(["--tlsauthtype", value, Url]);

        Diagnostics.AssertRefusal(result, CurlExitCode.FailedInit, ["curl: option --tlsauthtype: the installed libcurl version does not support this", TryHelp]);
        Assert.AreEqual(CurlExitCode.FailedInit, result.Refusal!.ExitCode);
        CollectionAssert.AreEqual(
            new[] { "curl: option --tlsauthtype: the installed libcurl version does not support this", TryHelp },
            result.Refusal.StandardErrorLines.ToArray());
    }

    [TestMethod]
    [DataRow(new[] { "--tls-earlydata" }, true)]
    [DataRow(new[] { "--tls-earlydata", "--no-tls-earlydata" }, false)]
    [DataRow(new[] { "--no-tls-earlydata", "--tls-earlydata" }, true)]
    public void Parse_TlsEarlyData_TheLastSpellingWins(string[] arguments, bool expected)
    {
        CommandLineParseResult result = Parse([.. arguments, Url]);

        Diagnostics.Assert("TLS early data", expected, CommandLineParseDiagnostics.Peek(result.Options)?.TlsEarlyData);
        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual(expected, result.Options.TlsEarlyData);
    }

    [TestMethod]
    [DataRow("--no-curves")]
    [DataRow("--no-sigalgs")]
    [DataRow("--no-ech")]
    [DataRow("--no-ssl-sessions")]
    [DataRow("--no-engine")]
    [DataRow("--no-dump-ca-embed")]
    [DataRow("--no-tlsuser")]
    [DataRow("--no-tlspassword")]
    [DataRow("--no-tlsauthtype")]
    public void Parse_NegatedValueOption_CannotBeReversed(string spelledOption)
    {
        CommandLineParseResult result = Parse([spelledOption, Url]);

        Diagnostics.AssertRefusal(
            result,
            CurlExitCode.FailedInit,
            [$"curl: option {spelledOption}: the given option cannot be reversed with a --no- prefix", TryHelp]);
        CollectionAssert.AreEqual(
            new[] { $"curl: option {spelledOption}: the given option cannot be reversed with a --no- prefix", TryHelp },
            result.Refusal!.StandardErrorLines.ToArray());
    }

    [TestMethod]
    public void Parse_SslSessionsFileLikeAFlag_WarnsAndKeepsIt()
    {
        CommandLineParseResult result = Parse(["--ssl-sessions", "-x", Url]);

        Diagnostics.Assert("SSL sessions file", "-x", CommandLineParseDiagnostics.Peek(result.Options)?.SslSessionsFile);
        AssertWarningLines(["Warning: The filename argument '-x' looks like a flag."], result);
        Assert.AreEqual("-x", result.Options!.SslSessionsFile);
        CollectionAssert.AreEqual(new[] { "Warning: The filename argument '-x' looks like a flag." }, result.WarningLines.ToArray());
    }

    [TestMethod]
    public void Parse_SslSessions_IsSharedByEveryGroup()
    {
        CommandLineParseResult result = Parse([Url, "--next", "--ssl-sessions", "sess.bin", Url]);

        Diagnostics.Act("group count", result.Groups.Count);
        Diagnostics.Assert("group 0 SSL sessions file", "sess.bin", result.Groups[0].SslSessionsFile);
        Diagnostics.Assert("group 1 SSL sessions file", "sess.bin", result.Groups[1].SslSessionsFile);
        Assert.AreEqual("sess.bin", result.Groups[0].SslSessionsFile);
        Assert.AreEqual("sess.bin", result.Groups[1].SslSessionsFile);
    }

    [TestMethod]
    [DataRow("true")]
    [DataRow("hard")]
    [DataRow("grease")]
    [DataRow("false")]
    [DataRow("bogus")]
    [DataRow("pn:x")]
    [DataRow("ecl:")]
    [DataRow("ecl:x")]
    public void Parse_EchKeyword_RecordsTheModeUnchecked(string value)
    {
        CommandLineParseResult result = Parse(["--ech", value, Url]);

        CommandLineOptions? recorded = CommandLineParseDiagnostics.Peek(result.Options);
        Diagnostics.Assert("ECH", value, recorded?.Ech);
        Diagnostics.Assert("ECH public name", null, recorded?.EchPublicName);
        Diagnostics.Assert("ECH config list", null, recorded?.EchConfigList);
        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual(value, result.Options.Ech);
        Assert.IsNull(result.Options.EchPublicName);
        Assert.IsNull(result.Options.EchConfigList);
    }

    [TestMethod]
    [DataRow("false")]
    [DataRow("grease")]
    [DataRow("true")]
    [DataRow("hard")]
    [DataRow("pn:x")]
    [DataRow("ecl:x")]
    public void Parse_EchModeLibcurlAccepts_IsNotMalformed(string value)
    {
        // libcurl's setopt_ech (curl 8.21.0) accepts the four keywords, "pn:" with a name and "ecl:" with a list.
        CommandLineOptions options = Parse(["--ech", value, Url]).Options!;

        Diagnostics.Assert("ECH mode is malformed", false, options.EchModeIsMalformed);
        Assert.IsFalse(options.EchModeIsMalformed);
    }

    [TestMethod]
    [DataRow("bogus")]
    [DataRow("TRUE")]
    [DataRow("Hard")]
    [DataRow("ecl:")]
    [DataRow("pn:")]
    [DataRow("PN:x")]
    [DataRow("ECL:x")]
    public void Parse_EchModeLibcurlRefuses_IsMalformed(string value)
    {
        // curl --ech bogus https://... -> curl: (43) setopt 0x2855 got bad argument (ECH build, BL-1107);
        // setopt_ech compares case-sensitively, so a keyword or prefix in another case is refused too.
        CommandLineParseResult result = Parse(["--ech", value, Url]);

        Diagnostics.Assert("ECH mode is malformed", true, CommandLineParseDiagnostics.Peek(result.Options)?.EchModeIsMalformed);
        Assert.IsTrue(result.IsAccepted);
        Assert.IsTrue(result.Options.EchModeIsMalformed);
    }

    [TestMethod]
    public void Parse_NoEchMode_IsNotMalformed()
    {
        CommandLineOptions options = Parse(["--ech", "pn:example.com", Url]).Options!;

        Diagnostics.Assert("ECH mode is malformed", false, options.EchModeIsMalformed);
        Assert.IsFalse(options.EchModeIsMalformed);
    }

    [TestMethod]
    [DataRow("pn:example.com", "example.com")]
    [DataRow("PN:ab", "ab")]
    public void Parse_EchPublicName_RecordsTheNameWithoutItsPrefix(string value, string expected)
    {
        CommandLineParseResult result = Parse(["--ech", "true", "--ech", value, Url]);

        CommandLineOptions? recorded = CommandLineParseDiagnostics.Peek(result.Options);
        Diagnostics.Assert("ECH public name", expected, recorded?.EchPublicName);
        Diagnostics.Assert("ECH", "true", recorded?.Ech);
        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual(expected, result.Options.EchPublicName);
        Assert.AreEqual("true", result.Options.Ech);
    }

    [TestMethod]
    [DataRow("ecl:AEX+DQ==", "AEX+DQ==")]
    [DataRow("ECL:ab", "ab")]
    public void Parse_EchConfigList_RecordsTheListWithoutItsPrefix(string value, string expected)
    {
        CommandLineParseResult result = Parse(["--ech", value, Url]);

        CommandLineOptions? recorded = CommandLineParseDiagnostics.Peek(result.Options);
        Diagnostics.Assert("ECH config list", expected, recorded?.EchConfigList);
        Diagnostics.Assert("ECH", null, recorded?.Ech);
        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual(expected, result.Options.EchConfigList);
        Assert.IsNull(result.Options.Ech);
    }

    [TestMethod]
    public void Parse_EchConfigListFromFile_ReadsItUpToANulWithoutLineBreaks()
    {
        RecordingDataFileReader reader = new();
        reader.Files["ech.txt"] = Encoding.UTF8.GetBytes("AEX+\r\nDQ==\n\0ignored");
        Diagnostics.Bytes("ech.txt", reader.Files["ech.txt"]);

        CommandLineParseResult result = Parse(["--ech", "ecl:@ech.txt", Url], reader);

        Diagnostics.Assert("ECH config list", "AEX+DQ==", CommandLineParseDiagnostics.Peek(result.Options)?.EchConfigList);
        Diagnostics.Assert("file reads", CommandLineParseDiagnostics.QuoteEach(["ech.txt"]), CommandLineParseDiagnostics.QuoteEach(reader.Reads));
        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual("AEX+DQ==", result.Options!.EchConfigList);
        CollectionAssert.AreEqual(new[] { "ech.txt" }, reader.Reads);
    }

    [TestMethod]
    public void Parse_EchConfigListFromStandardInput_ReadsStandardInput()
    {
        RecordingDataFileReader reader = new() { StandardInput = Encoding.UTF8.GetBytes("AEX+DQ==\n") };
        Diagnostics.Bytes("standard input", reader.StandardInput);

        CommandLineParseResult result = Parse(["--ech", "ecl:@-", Url], reader);

        Diagnostics.Assert("ECH config list", "AEX+DQ==", CommandLineParseDiagnostics.Peek(result.Options)?.EchConfigList);
        Diagnostics.Assert("file reads", CommandLineParseDiagnostics.QuoteEach(["-"]), CommandLineParseDiagnostics.QuoteEach(reader.Reads));
        Assert.AreEqual("AEX+DQ==", result.Options!.EchConfigList);
        CollectionAssert.AreEqual(new[] { "-" }, reader.Reads);
    }

    [TestMethod]
    public void Parse_EchConfigListFromUnreadableFile_WarnsAndRefusesAsBadlyUsed()
    {
        CommandLineParseResult result = Parse(["--ech", "ecl:@missing.txt", Url], new RecordingDataFileReader());

        AssertWarningLines(["Warning: Could not read file \"missing.txt\" specified for \"--ech ecl:\" option"], result);
        Diagnostics.AssertRefusal(result, CurlExitCode.FailedInit, ["curl: option --ech: is badly used here", TryHelp]);
        Assert.AreEqual(CurlExitCode.FailedInit, result.Refusal!.ExitCode);
        CollectionAssert.AreEqual(
            new[] { "Warning: Could not read file \"missing.txt\" specified for \"--ech ecl:\" option" },
            result.WarningLines.ToArray());
        CollectionAssert.AreEqual(
            new[] { "curl: option --ech: is badly used here", TryHelp },
            result.Refusal.StandardErrorLines.ToArray());
    }

    [TestMethod]
    public void Parse_EchConfigListFromUnreadableFileUnderSilent_RefusesWithoutTheWarning()
    {
        CommandLineParseResult result = Parse(["-s", "--ech", "ecl:@missing.txt", Url], new RecordingDataFileReader());

        Diagnostics.Assert("accepted", false, result.IsAccepted);
        AssertWarningLines([], result);
        Assert.IsFalse(result.IsAccepted);
        Assert.IsEmpty(result.WarningLines);
    }

    [TestMethod]
    [DataRow("--engine", "list")]
    [DataRow("--engine=list", null)]
    public void Parse_EngineList_EndsTheCommandLineAsARequestForInformation(string option, string? value)
    {
        string[] arguments = value is null ? [option, "--bogus"] : [option, value, "--bogus"];

        CommandLineParseResult result = Parse(arguments);

        CommandLineOptions? recorded = CommandLineParseDiagnostics.Peek(result.Options);
        Diagnostics.Assert("engine list requested", true, recorded?.EngineListRequested);
        Diagnostics.Assert("engine", "list", recorded?.Engine);
        Assert.IsTrue(result.IsAccepted);
        Assert.IsTrue(result.Options.EngineListRequested);
        Assert.AreEqual("list", result.Options.Engine);
    }

    [TestMethod]
    public void Parse_EngineListCased_IsAnEngineName()
    {
        CommandLineParseResult result = Parse(["--engine", "LIST", Url]);

        CommandLineOptions? recorded = CommandLineParseDiagnostics.Peek(result.Options);
        Diagnostics.Assert("engine list requested", false, recorded?.EngineListRequested);
        Diagnostics.Assert("engine", "LIST", recorded?.Engine);
        Assert.IsTrue(result.IsAccepted);
        Assert.IsFalse(result.Options.EngineListRequested);
        Assert.AreEqual("LIST", result.Options.Engine);
    }

    [TestMethod]
    public void Parse_DumpCaEmbed_EndsTheCommandLineAsARequestForInformation()
    {
        CommandLineParseResult result = Parse(["--dump-ca-embed", "--bogus"]);

        Diagnostics.Assert("CA embed dump requested", true, CommandLineParseDiagnostics.Peek(result.Options)?.CaEmbedDumpRequested);
        Assert.IsTrue(result.IsAccepted);
        Assert.IsTrue(result.Options.CaEmbedDumpRequested);
    }

    [TestMethod]
    [DataRow("engine list")]
    [DataRow("dump-ca-embed")]
    public void Parse_InformationRequestInConfigFile_IsIgnored(string line)
    {
        RecordingDataFileReader reader = new();
        reader.Files["k.txt"] = Encoding.UTF8.GetBytes(line + "\n");
        Diagnostics.Bytes("k.txt", reader.Files["k.txt"]);

        CommandLineParseResult result = Parse(["-K", "k.txt", Url], reader);

        CommandLineOptions? recorded = CommandLineParseDiagnostics.Peek(result.Options);
        Diagnostics.Assert("engine list requested", false, recorded?.EngineListRequested);
        Diagnostics.Assert("CA embed dump requested", false, recorded?.CaEmbedDumpRequested);
        Assert.IsTrue(result.IsAccepted);
        Assert.IsFalse(result.Options.EngineListRequested);
        Assert.IsFalse(result.Options.CaEmbedDumpRequested);
    }

    private CommandLineParseResult Parse(IReadOnlyList<string> arguments) =>
        WriteDiagnostics(arguments, () => OpenSslBuildParser.Parse(arguments));

    private CommandLineParseResult Parse(IReadOnlyList<string> arguments, IDataFileReader reader) =>
        WriteDiagnostics(arguments, () => OpenSslBuildParser.Parse(arguments, _ => true, new UnexpectedPasswordPrompt(), reader));

    private CommandLineParseResult WriteDiagnostics(IReadOnlyList<string> arguments, Func<CommandLineParseResult> parse)
    {
        Diagnostics.ArrangeArguments(arguments);
        CommandLineParseResult result = parse();
        Diagnostics.ActParse(result);
        if (result.IsAccepted)
        {
            CommandLineOptions options = result.Options;
            Diagnostics.Act("curves", options.Curves);
            Diagnostics.Act("signature algorithms", options.SignatureAlgorithms);
            Diagnostics.Act("TLS early data", options.TlsEarlyData);
            Diagnostics.Act("ECH", options.Ech);
            Diagnostics.Act("ECH public name", options.EchPublicName);
            Diagnostics.Act("ECH config list", options.EchConfigList);
            Diagnostics.Act("ECH mode is malformed", options.EchModeIsMalformed);
            Diagnostics.Act("SSL sessions file", options.SslSessionsFile);
            Diagnostics.Act("engine", options.Engine);
            Diagnostics.Act("engine list requested", options.EngineListRequested);
            Diagnostics.Act("CA embed dump requested", options.CaEmbedDumpRequested);
            Diagnostics.Act("TLS user", options.TlsUser);
            Diagnostics.Act("TLS password", options.TlsPassword);
            Diagnostics.Act("TLS auth type", options.TlsAuthType);
        }

        return result;
    }

    private void AssertWarningLines(IEnumerable<string> expected, CommandLineParseResult result) =>
        Diagnostics.Assert("warning lines", CommandLineParseDiagnostics.QuoteEach(expected), CommandLineParseDiagnostics.QuoteEach(result.WarningLines));

    private sealed class UnexpectedPasswordPrompt : IPasswordPrompt
    {
        public string ReadPassword(string prompt) => throw new AssertFailedException("No password prompt was expected.");
    }
}
