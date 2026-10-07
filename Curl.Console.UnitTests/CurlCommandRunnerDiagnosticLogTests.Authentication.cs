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
        string[] arguments = ["--anyauth", "-u", "user:s3cret", "--log-level", "verbose", "--log-file", logFile, Url];
        Diagnostics.Arrange("command line", CommandLine(arguments));

        int exitCode;
        using (Diagnostics.Phase("run"))
        {
            exitCode = await CurlComposition
                .CreateRunner(output, error, input, server, new RecordingDatagramConnector(CurlExitCode.CouldntConnect, "unused"))
                .RunAsync(arguments);
        }

        Diagnostics.Act("exit code", exitCode);
        Diagnostics.Bytes("stdout", output.ToArray());
        Diagnostics.Bytes("stderr", error.ToArray());
        Diagnostics.Bytes("request bytes", server.Written);
        string log = File.ReadAllText(logFile);
        Diagnostics.Assert(
            "log names the chosen scheme",
            true,
            log.Contains("server offered Basic, Digest; allowed Any; chose Digest", StringComparison.Ordinal));
        Diagnostics.Assert(
            "log names the Digest algorithm",
            true,
            log.Contains("Digest algorithm MD5 (not named), qop none", StringComparison.Ordinal));
        Diagnostics.Assert("log has the password", false, log.Contains("s3cret", StringComparison.Ordinal));
        StringAssert.Contains(log, "server offered Basic, Digest; allowed Any; chose Digest");
        StringAssert.Contains(log, "Digest algorithm MD5 (not named), qop none");
        Assert.DoesNotContain("s3cret", log);
    }

    [TestMethod]
    public async Task RunAsync_NetrcFileAndAwsSigV4AtLogLevelInfo_LogsTheNetrcMatchAndTheScopeButNotThePassword()
    {
        string directory = CreateTemporaryDirectory();
        string logFile = Path.Combine(directory, "auth.log");
        string netrcFile = Path.Combine(directory, "netrc");
        File.WriteAllText(netrcFile, "machine 127.0.0.1 login alice password s3cret\n");
        ScriptedConnector server = new([Encoding.Latin1.GetBytes("HTTP/1.1 200 OK\r\nContent-Length: 5\r\n\r\nhello")]);
        using MemoryStream output = new();
        using MemoryStream error = new();
        using MemoryStream input = new();
        string[] arguments = ["--netrc-file", netrcFile, "--aws-sigv4", "aws:amz:us-east-1:s3", "--log-level", "info", "--log-file", logFile, Url];
        Diagnostics.Arrange("command line", CommandLine(arguments));
        Diagnostics.Arrange("netrc file", "machine 127.0.0.1 login alice password s3cret");

        int exitCode;
        using (Diagnostics.Phase("run"))
        {
            exitCode = await CurlComposition
                .CreateRunner(output, error, input, server, new RecordingDatagramConnector(CurlExitCode.CouldntConnect, "unused"))
                .RunAsync(arguments);
        }

        Diagnostics.Act("exit code", exitCode);
        Diagnostics.Bytes("stdout", output.ToArray());
        Diagnostics.Bytes("stderr", error.ToArray());
        Diagnostics.Bytes("request bytes", server.Written);
        string log = File.ReadAllText(logFile);
        Diagnostics.Assert(
            "log names the netrc match",
            true,
            log.Contains("netrc entry matched for host 127.0.0.1, login alice", StringComparison.Ordinal));
        Diagnostics.Assert(
            "log names the AWS SigV4 scope",
            true,
            log.Contains("AWS SigV4 scope: provider aws:amz, region us-east-1, service s3", StringComparison.Ordinal));
        Diagnostics.Assert("log has the password", false, log.Contains("s3cret", StringComparison.Ordinal));
        StringAssert.Contains(log, "netrc entry matched for host 127.0.0.1, login alice");
        StringAssert.Contains(log, "AWS SigV4 scope: provider aws:amz, region us-east-1, service s3");
        Assert.DoesNotContain("s3cret", log);
    }
}
