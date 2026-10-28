using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Cli;

/// <summary>
/// Pins CGR-043 part 3 (BL-3889): <c>--proto-default</c> does not know <c>smb</c> or <c>smbs</c>, as neither
/// reference build of curl 8.22.0 has SMB. Measured with <c>Record-CurlExchange.ps1</c> against the Windows
/// curl on 2026-10-28: <c>curl --proto-default smb -sS 127.0.0.1:20002/share/x</c> exits 1, writes nothing to
/// standard output, sends no byte, and writes <c>curl: option --proto-default: a specified protocol is
/// unsupported by libcurl</c> and the try-help line.
/// </summary>
[TestClass]
public sealed class Cgr043SmbProtoDefaultTests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    [DataRow("smb", DisplayName = "the reported command")]
    [DataRow("smbs", DisplayName = "smbs")]
    [DataRow("SMB", DisplayName = "upper case")]
    public void Parse_ProtoDefaultNamingSmb_RefusesItAsUnsupportedWithExitOne(string value)
    {
        string[] arguments = ["--proto-default", value, "-sS", "127.0.0.1:20002/share/x"];
        Diagnostics.ArrangeArguments(arguments);

        CommandLineParseResult result = CommandLineParser.Parse(arguments);
        Diagnostics.ActParse(result);

        string[] expectedLines =
        [
            "curl: option --proto-default: a specified protocol is unsupported by libcurl",
            CommandLineRefusal.TryHelpLine,
        ];
        CommandLineRefusal? refusal = CommandLineParseDiagnostics.Peek(result.Refusal);
        Diagnostics.Assert("exit code", CurlExitCode.UnsupportedProtocol, refusal?.ExitCode);
        Diagnostics.Assert(
            "stderr",
            CommandLineParseDiagnostics.QuoteEach(expectedLines),
            CommandLineParseDiagnostics.QuoteEach(refusal?.StandardErrorLines ?? []));
        Assert.IsFalse(result.IsAccepted);
        Assert.AreEqual(CurlExitCode.UnsupportedProtocol, result.Refusal.ExitCode);
        CollectionAssert.AreEqual(expectedLines, result.Refusal.StandardErrorLines.ToArray());
    }
}
