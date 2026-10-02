using System.Text;
using Curl.Protocol.Abstractions;

namespace Curl.Console;

/// <summary>
/// Pins that the composed authenticators write to the run's diagnostic log (BL-923): the scheme
/// <c>--anyauth</c> picks from a 401 reaches the <c>--log-file</c>, and the password does not.
/// </summary>
public sealed partial class CurlCommandRunnerDiagnosticLogTests
{
    [TestMethod]
    public async Task RunAsync_AnyAuthAtLogLevelVerbose_LogsTheChosenSchemeButNotThePassword()
    {
        string logFile = Path.Combine(CreateTemporaryDirectory(), "auth.log");
        ScriptedConnector server = new(
        [
            Encoding.Latin1.GetBytes("HTTP/1.1 401 Unauthorized\r\nWWW-Authenticate: Basic realm=\"r\"\r\nWWW-Authenticate: Digest realm=\"r\", nonce=\"n\"\r\nContent-Length: 0\r\n\r\n"),
            Encoding.Latin1.GetBytes("HTTP/1.1 200 OK\r\nContent-Length: 5\r\n\r\nhello"),
        ]);
        using MemoryStream output = new();
        using MemoryStream error = new();
        using MemoryStream input = new();

        await CurlComposition
            .CreateRunner(output, error, input, server, new RecordingDatagramConnector(CurlExitCode.CouldntConnect, "unused"))
            .RunAsync(["--anyauth", "-u", "user:s3cret", "--log-level", "verbose", "--log-file", logFile, Url]);

        string log = File.ReadAllText(logFile);
        StringAssert.Contains(log, "server offered Basic, Digest; allowed Any; chose Digest");
        StringAssert.Contains(log, "Digest algorithm MD5 (not named), qop none");
        Assert.DoesNotContain("s3cret", log);
    }
}
