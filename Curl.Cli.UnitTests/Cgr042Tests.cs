using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Cli;

/// <summary>
/// Pins <c>--ech</c> as every reference build answers it (third-party gap report issue CGR-042, ADR-0918): curl
/// 8.22.0's Schannel build, measured on 2026-10-26 (BL-3571), and the OpenSSL build from the release tarball, built
/// without ECH, refuse it with exit 2 and <c>curl: option --ech: the installed libcurl version does not support
/// this</c>, whatever its value, so Curl refuses it on every platform unless the parse reads as an ECH build
/// (<see cref="CommandLineOptions.ActsAsEchBuild"/>), which only tests and the conformance harness ask for.
/// </summary>
[TestClass]
public sealed class Cgr042Tests
{
    private const string Url = "http://127.0.0.1:20000/";
    private const string Refusal = "curl: option --ech: the installed libcurl version does not support this";

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    [DataRow(new[] { "--ech", "true", "-s", Url }, false)]
    [DataRow(new[] { "--ech", "true", "-s", Url }, true)]
    [DataRow(new[] { "-sS", "--ech", "0", "-o", "nUL", "http://127.0.0.1:20002/" }, false)]
    [DataRow(new[] { "-sS", "--ech", "0", "-o", "nUL", "http://127.0.0.1:20002/" }, true)]
    [DataRow(new[] { "--ech", "pn:example.com", Url }, false)]
    [DataRow(new[] { "--ech", "", Url }, true)]
    public void Parse_EchOnAReferenceBuild_IsRefusedAsnotSupported(string[] arguments, bool isWindows)
    {
        Diagnostics.Arrange("is windows", isWindows);
        Diagnostics.ArrangeArguments(arguments);

        CommandLineParseResult result = CommandLineParser.Parse(arguments, _ => true, new EmptyPasswordPrompt(), new RecordingDataFileReader(), isWindows);

        Diagnostics.ActParse(result);
        Diagnostics.AssertRefusal(result, CurlExitCode.FailedInit, [Refusal, CommandLineRefusal.TryHelpLine]);
        Assert.IsFalse(result.IsAccepted);
        Assert.AreEqual(CurlExitCode.FailedInit, result.Refusal.ExitCode);
        CollectionAssert.AreEqual(new[] { Refusal, CommandLineRefusal.TryHelpLine }, result.Refusal.StandardErrorLines.ToArray());
    }

    [TestMethod]
    public void Parse_EchOnAnEchBuild_IsAcceptedWithItsMode()
    {
        string[] arguments = ["--ech", "true", Url];
        Diagnostics.ArrangeArguments(arguments);

        CommandLineParseResult result = EchBuildParser.Parse(arguments);

        Diagnostics.ActParse(result);
        Diagnostics.Assert("ECH", "true", result.IsAccepted ? result.Options.Ech : null);
        Assert.IsTrue(result.IsAccepted);
        Assert.IsTrue(result.Options.ActsAsEchBuild);
        Assert.AreEqual("true", result.Options.Ech);
    }

    [TestMethod]
    public void Parse_WithoutAskingForAnEchBuild_DoesnotActAsOne()
    {
        CommandLineParseResult result = CommandLineParser.Parse([Url], _ => true, new EmptyPasswordPrompt(), new RecordingDataFileReader(), isWindows: false);

        Diagnostics.ActParse(result);
        Diagnostics.Assert("acts as ECH build", false, result.IsAccepted && result.Options.ActsAsEchBuild);
        Assert.IsTrue(result.IsAccepted);
        Assert.IsFalse(result.Options.ActsAsEchBuild);
    }

    [TestMethod]
    public void AiHelp_EchSection_SaysEveryPlatformRefusesIt()
    {
        Assert.IsTrue(CurlAiHelpText.TryGetMarkdown("all", out string help));
        int start = help.IndexOf("## --ech\n", StringComparison.Ordinal);
        int end = help.IndexOf("\n## ", start + 1, StringComparison.Ordinal);
        string section = help[start..(end < 0 ? help.Length : end)];

        Diagnostics.Assert("section says every platform refuses", true, section.Contains("- On every platform: refused with exit 2", StringComparison.Ordinal));
        StringAssert.Contains(section, "- On every platform: refused with exit 2 (`the installed libcurl version does not support this`), as curl's reference builds (Schannel on Windows, OpenSSL on Linux and macOS) are built without ECH.");
    }
}
