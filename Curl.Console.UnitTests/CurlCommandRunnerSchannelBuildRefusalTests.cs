using System.Text;
using Curl.Networking;
using Curl.Protocol.Abstractions;

namespace Curl.Console;

/// <summary>
/// Pins the runner reading a command line as curl's Windows Schannel build does (ADR-0395, audit finding
/// AF-0022): <c>--http2</c>, <c>--tlsuser</c> and <c>--ssl-sessions</c> are refused with exit 2 and
/// <c>the installed libcurl version does not support this</c>, even under <c>-s</c>, and nothing is
/// connected - measured with curl 8.21.0 (x86_64-w64-mingw32) Schannel on 2026-10-02 (BL-1278).
/// </summary>
[TestClass]
public sealed class CurlCommandRunnerSchannelBuildRefusalTests
{
    private const string Url = "http://127.0.0.1:48295/";

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

        int exitCode = await CurlComposition
            .CreateRunner(standardOutput, standardError, standardInput, connector, new RecordingDatagramConnector(CurlExitCode.CouldntConnect, "unused"), parsesAsWindowsBuild: true)
            .RunAsync(["-s", .. optionArguments, Url]);

        Assert.AreEqual(2, exitCode);
        Assert.IsEmpty(connector.Targets);
        Assert.AreEqual(
            $"curl: option {optionArguments[0]}: the installed libcurl version does not support this{Environment.NewLine}"
            + $"curl: try 'curl --help' or 'curl --manual' for more information{Environment.NewLine}",
            Encoding.Latin1.GetString(standardError.ToArray()));
        Assert.AreEqual(0L, standardOutput.Length);
    }
}
