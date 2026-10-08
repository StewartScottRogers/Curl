using System.Text;
using Curl.Networking;
using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Console;

/// <summary>
/// Pins the runner reading a command line as curl's Windows Schannel build does (ADR-0397, audit finding
/// AF-0022): <c>--http2</c>, <c>--tlsuser</c> and <c>--ssl-sessions</c> are refused with exit 2 and
/// <c>the installed libcurl version does not support this</c>, even under <c>-s</c>, and nothing is
/// connected - measured with curl 8.21.0 (x86_64-w64-mingw32) Schannel on 2026-10-02 (BL-1278).
/// </summary>
[TestClass]
public sealed class CurlCommandRunnerSchannelBuildRefusalTests
{
    private const string Url = "http://127.0.0.1:48295/";

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    [DataRow("--http2")]
    [DataRow("--tlsuser|1")]
    [DataRow("--ssl-sessions|f.txt")]
    public async Task RunAsync_OptionTheWindowsBuildLacks_ExitsTwoWithoutConnecting(string option)
    {
        using MemoryStream standardOutput = new();
        using MemoryStream standardError = new();
        using MemoryStream standardInput = new();
        RecordingConnector connector = new(CurlExitCode.CouldntConnect, "unused");
        string[] optionArguments = option.Split('|');

        Diagnostics.Arrange("arguments", string.Join(' ', ["-s", .. optionArguments, Url]));
        Diagnostics.Arrange("build", "parses as the Windows Schannel build; connector fails with CouldntConnect");
        int exitCode;
        using (Diagnostics.Phase("run"))
        {
            exitCode = await CurlComposition
                .CreateRunner(standardOutput, standardError, standardInput, connector, new RecordingDatagramConnector(CurlExitCode.CouldntConnect, "unused"), parsesAsWindowsBuild: true)
                .RunAsync(["-s", .. optionArguments, Url]);
        }

        string errorText = Encoding.Latin1.GetString(standardError.ToArray()).Replace("\r\n", "\n", StringComparison.Ordinal);
        Diagnostics.Act("exit code", exitCode);
        Diagnostics.Act("connection targets", connector.Targets.Count);
        Diagnostics.Act("stderr", errorText);
        Diagnostics.Assert("exit code", 2, exitCode);
        Diagnostics.Assert("connection targets", 0, connector.Targets.Count);
        Diagnostics.Diff(
            "stderr",
            $"curl: option {optionArguments[0]}: the installed libcurl version does not support this\ncurl: try 'curl --help' or 'curl --manual' for more information\n",
            errorText);
        Assert.AreEqual(2, exitCode);
        Assert.IsEmpty(connector.Targets);
        Assert.AreEqual(
            $"curl: option {optionArguments[0]}: the installed libcurl version does not support this{Environment.NewLine}"
            + $"curl: try 'curl --help' or 'curl --manual' for more information{Environment.NewLine}",
            Encoding.Latin1.GetString(standardError.ToArray()));
        Assert.AreEqual(0L, standardOutput.Length);
    }
}
