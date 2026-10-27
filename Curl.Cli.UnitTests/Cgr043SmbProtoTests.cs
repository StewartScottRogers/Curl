using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Cli;

/// <summary>
/// Pins CGR-043 part 2 (BL-3888): <c>--proto</c> and <c>--proto-redir</c> do not know <c>smb</c> or
/// <c>smbs</c>, as neither reference build of curl 8.22.0 has SMB. Measured with
/// <c>Record-CurlExchange.ps1</c> against the Windows curl on 2026-10-26:
/// <c>curl --proto =smb -sS smb://127.0.0.1:20002/share/x</c> exits 2, writes nothing to standard output,
/// sends no byte, and writes <c>curl: unrecognized protocol 'smb'</c>, <c>curl: option --proto: is badly used
/// here</c> and the try-help line.
/// </summary>
[TestClass]
public sealed class Cgr043SmbProtoTests
{
    private const string SmbUrl = "smb://127.0.0.1:20002/share/x";

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    [DataRow("--proto", "=smb", "smb", DisplayName = "the reported command")]
    [DataRow("--proto", "smbs", "smbs", DisplayName = "smbs")]
    [DataRow("--proto", "http,=SMB", "SMB", DisplayName = "repeated as typed")]
    [DataRow("--proto", "-smb", "smb", DisplayName = "removing smb")]
    [DataRow("--proto-redir", "=smb", "smb", DisplayName = "proto-redir")]
    public void Parse_ProtoNamingSmb_RefusesItAsUnrecognized(string option, string value, string expectedName)
    {
        string[] arguments = [option, value, "-sS", SmbUrl];
        Diagnostics.ArrangeArguments(arguments);

        CommandLineParseResult result = CommandLineParser.Parse(arguments);
        Diagnostics.ActParse(result);

        string[] expectedLines =
        [
            $"curl: unrecognized protocol '{expectedName}'",
            $"curl: option {option}: is badly used here",
            CommandLineRefusal.TryHelpLine,
        ];
        CommandLineRefusal? refusal = CommandLineParseDiagnostics.Peek(result.Refusal);
        Diagnostics.Assert("exit code", CurlExitCode.FailedInit, refusal?.ExitCode);
        Diagnostics.Assert(
            "stderr",
            CommandLineParseDiagnostics.QuoteEach(expectedLines),
            CommandLineParseDiagnostics.QuoteEach(refusal?.StandardErrorLines ?? []));
        Assert.IsFalse(result.IsAccepted);
        Assert.AreEqual(CurlExitCode.FailedInit, result.Refusal.ExitCode);
        CollectionAssert.AreEqual(expectedLines, result.Refusal.StandardErrorLines.ToArray());
    }

    [TestMethod]
    [DataRow("all")]
    [DataRow("-ftp")]
    public void Parse_ProtoAllowingEveryScheme_LeavesSmbAndSmbsOut(string value)
    {
        string[] arguments = ["--proto", value, SmbUrl];
        Diagnostics.ArrangeArguments(arguments);

        CommandLineParseResult result = CommandLineParser.Parse(arguments);
        Diagnostics.ActParse(result);

        Assert.IsTrue(result.IsAccepted);
        IReadOnlySet<string>? allowed = CommandLineParseDiagnostics.Peek(result.Options.AllowedProtocols);
        Diagnostics.Assert("smb allowed", false, allowed?.Contains("smb"));
        Diagnostics.Assert("smbs allowed", false, allowed?.Contains("smbs"));
        Assert.IsFalse(result.Options.AllowedProtocols!.Contains("smb"));
        Assert.IsFalse(result.Options.AllowedProtocols.Contains("smbs"));
    }
}
