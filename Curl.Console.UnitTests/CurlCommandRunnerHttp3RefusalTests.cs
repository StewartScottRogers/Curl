using System.Text;
using Curl.Networking;
using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Console;

/// <summary>
/// Pins the runner reading a command line as curl 8.22.0's OpenSSL reference build does on Linux and macOS (gap
/// finding GF-0089, ADR-0540): with no QUIC library, <c>--http3</c> and <c>--http3-only</c> are refused with exit 2 and
/// <c>the installed libcurl version does not support this</c>, even under <c>-s</c>, and nothing is
/// connected, as the gap-platforms workflow measured on Linux (BL-2332). Only a runner asked to parse as a QUIC build accepts them.
/// </summary>
[TestClass]
public sealed class CurlCommandRunnerHttp3RefusalTests
{
    private const string Url = "http://127.0.0.1:48295/";

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    [DataRow("--http3")]
    [DataRow("--http3-only")]
    public async Task RunAsync_Http3OptionOnTheOpenSslBuild_ExitsTwoWithoutConnecting(string option)
    {
        using MemoryStream standardOutput = new();
        using MemoryStream standardError = new();
        using MemoryStream standardInput = new();
        RecordingConnector connector = new(CurlExitCode.CouldntConnect, "unused");
        string[] optionArguments = option.Split('|');

        Diagnostics.Arrange("arguments", string.Join(' ', ["-s", .. optionArguments, Url]));
        Diagnostics.Arrange("build", "parses as the OpenSSL build, not a QUIC build; connector fails with CouldntConnect");
        int exitCode;
        using (Diagnostics.Phase("run"))
        {
            exitCode = await CurlComposition
                .CreateRunner(standardOutput, standardError, standardInput, connector, new RecordingDatagramConnector(CurlExitCode.CouldntConnect, "unused"), parsesAsWindowsBuild: false)
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
