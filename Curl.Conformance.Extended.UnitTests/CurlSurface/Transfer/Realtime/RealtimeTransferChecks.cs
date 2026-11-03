using System.Security.Cryptography.X509Certificates;
using System.Text;
using Curl.Conformance.Extended.Harness;
using Curl.Console;

namespace Curl.Conformance.Extended.UnitTests.CurlSurface.Transfer.Realtime;

/// <summary>
/// curl's generic transfer options checked for one realtime scheme, each against curl's manual page for curl 8.22.0
/// (curl.se/docs/manpage.html: "-v", "-o", "-O", "-w", "--fail", "--fail-with-body", "-m" and "EXIT CODES") and the
/// scheme's extension ADRs, run in process through the harness on a clock that moves only when a check advances it.
/// </summary>
internal static class RealtimeTransferChecks
{
    /// <summary>Every variable the -w check asks for, '|' between them.</summary>
    public const string WriteOutFormat = "%{exitcode}|%{errormsg}|%{url}|%{url_effective}|%{scheme}|%{remote_ip}|%{remote_port}|%{size_download}|%{time_total}\n";

    private const int MaxTimeSeconds = 100;

    private static readonly KnownFailures Known = KnownFailures.Load(Path.Combine(AppContext.BaseDirectory, "KnownFailures.txt"));

    /// <summary>
    /// The line curl's TLS backend writes under -v once the handshake is done: the Schannel build on Windows, the
    /// OpenSSL build elsewhere (the lane rules, "Decisions").
    /// </summary>
    private static string TlsLine => OperatingSystem.IsWindows() ? "* schannel: " : "* SSL connection using ";

    /// <summary>-v: "Makes curl verbose during the operation"; its lines are on standard error and the output is unchanged.</summary>
    public static Task VerboseAsync(RealtimeTransferScheme scheme) =>
        RunCaseAsync(scheme, "verbose", async directory =>
        {
            CurlRunResult result = await RunAnsweredAsync(scheme, directory, ["-sS", "-v"]);

            string error = Text(result.StandardError);
            Assert.AreEqual(0, result.ExitCode, error);
            Assert.AreEqual(scheme.Body, Text(result.StandardOutput), "manual, -v: nothing extra on standard output");
            StringAssert.Contains(error, $"* Established connection to localhost (127.0.0.1 port {scheme.Port}) from ", "manual, -v: curl 8.22.0's connection line");
            if (scheme.Transport != RealtimeTransport.Clear)
            {
                StringAssert.Contains(error, TlsLine, "manual, -v: the TLS lines");
            }
        });

    /// <summary>-o: "Write output to the given file instead of stdout".</summary>
    public static Task OutputAsync(RealtimeTransferScheme scheme) =>
        RunCaseAsync(scheme, "output", async directory =>
        {
            string file = Path.Combine(directory, "out.txt");

            CurlRunResult result = await RunAnsweredAsync(scheme, directory, ["-sS", "-o", file]);

            Assert.AreEqual(0, result.ExitCode, Text(result.StandardError));
            Assert.AreEqual(string.Empty, Text(result.StandardOutput), "manual, -o: nothing on standard output");
            Assert.AreEqual(scheme.Body, await File.ReadAllTextAsync(file), "manual, -o: the body in the file");
        });

    /// <summary>-O: "Write output to a local file named like the remote file", the last segment of the URL's path, here under --output-dir.</summary>
    public static Task RemoteNameAsync(RealtimeTransferScheme scheme) =>
        RunCaseAsync(scheme, "remote-name", async directory =>
        {
            CurlRunResult result = await RunAnsweredAsync(scheme, directory, ["-sS", "--output-dir", directory, "-O"]);

            Assert.AreEqual(0, result.ExitCode, Text(result.StandardError));
            Assert.AreEqual(string.Empty, Text(result.StandardOutput), "manual, -O: nothing on standard output");
            Assert.AreEqual(scheme.Body, await File.ReadAllTextAsync(Path.Combine(directory, scheme.RemoteName)), "manual, -O: the body in the file named after the URL");
        });

    /// <summary>
    /// -w: each variable as the manual defines it, after the body; %{time_total} is 0.000000 on a clock that never moves
    /// and %{size_download} the bytes written.
    /// </summary>
    public static Task WriteOutAsync(RealtimeTransferScheme scheme) =>
        RunCaseAsync(scheme, "write-out", async directory =>
        {
            CurlRunResult result = await RunAnsweredAsync(scheme, directory, ["-sS", "-w", WriteOutFormat]);

            Assert.AreEqual(0, result.ExitCode, Text(result.StandardError));
            string expected = $"0||{scheme.Url}|{scheme.Url}|{scheme.Scheme}|127.0.0.1|{scheme.Port}|{Encoding.UTF8.GetByteCount(scheme.Body)}|0.000000\n";
            Assert.AreEqual(scheme.Body + expected, Text(result.StandardOutput), "manual, -w");
        });

    /// <summary>--fail and --fail-with-body: a server error the extension maps to exit 22 (manual, "EXIT CODES" 22) ends the transfer with it and its message.</summary>
    public static Task FailAsync(RealtimeTransferScheme scheme, string option) =>
        RunCaseAsync(scheme, option.TrimStart('-'), async directory =>
        {
            CurlRunResult result = await RunAsync(
                scheme,
                scheme.Failing(),
                ["-sS", option, "-w", "%{exitcode}:%{errormsg}", .. await scheme.FailingArguments(directory), scheme.Url],
                new RealtimeTransferClock());

            Assert.AreEqual(22, result.ExitCode, Text(result.StandardError));
            Assert.AreEqual($"curl: (22) {scheme.FailureMessage}\n", Text(result.StandardError), "manual, EXIT CODES 22");
            Assert.AreEqual($"{scheme.FailureBody}22:{scheme.FailureMessage}", Text(result.StandardOutput), "manual, -w %{exitcode} and %{errormsg}");
        });

    /// <summary>-m: a server that never answers ends the transfer with exit 28 at the deadline on the injected clock (manual, "EXIT CODES" 28).</summary>
    public static Task MaxTimeAsync(RealtimeTransferScheme scheme) =>
        RunCaseAsync(scheme, "max-time", async directory =>
        {
            RealtimeTransferClock clock = new();
            NeverAnsweringServer server = new();
            string[] arguments = ["-sS", "-m", $"{MaxTimeSeconds}", .. await scheme.AnsweringArguments(directory), scheme.Url];

            Task<CurlRunResult> run = RunAsync(scheme, server, arguments, clock);
            await server.Reached.WaitAsync(TimeSpan.FromSeconds(30));
            // Until the run ends, not for -m + 1 seconds: curl may reach a clock timer only after the deadline (BL-4663).
            CurlRunResult result = await MaxTimeDeadlineRun.AdvanceUntilDoneAsync(run, clock.Advance);
            string error = Text(result.StandardError);
            Assert.AreEqual(28, result.ExitCode, error);
            StringAssert.StartsWith(error, "curl: (28) ", "manual, EXIT CODES 28");
            Assert.AreEqual(string.Empty, Text(result.StandardOutput));
        });

    private static Task RunCaseAsync(RealtimeTransferScheme scheme, string check, Func<string, Task> body) =>
        Known.RunCaseAsync(
            [scheme.Requirement(check)],
            async () =>
            {
                string directory = Directory.CreateTempSubdirectory("realtime-transfer-").FullName;
                try
                {
                    await body(directory);
                }
                finally
                {
                    Directory.Delete(directory, recursive: true);
                }
            });

    private static async Task<CurlRunResult> RunAnsweredAsync(RealtimeTransferScheme scheme, string directory, string[] options) =>
        await RunAsync(scheme, scheme.Answering(), [.. options, .. await scheme.AnsweringArguments(directory), scheme.Url], new RealtimeTransferClock());

    private static async Task<CurlRunResult> RunAsync(RealtimeTransferScheme scheme, IReferenceServer server, string[] arguments, TimeProvider clock)
    {
        using X509Certificate2? certificate = scheme.Transport == RealtimeTransport.Clear ? null : ReferenceCertificate.CreateSelfSigned("localhost");
        ReferenceRoute route = scheme.Transport switch
        {
            RealtimeTransport.Clear => ReferenceRoute.Tcp(scheme.Port, server),
            RealtimeTransport.Tls => ReferenceRoute.Tls(scheme.Port, server, certificate!),
            _ => ReferenceRoute.Tcp(scheme.Port, new AlpnH2TlsServer(server, certificate!, scheme.Port)),
        };
        string[] trust = scheme.Transport == RealtimeTransport.Clear ? [] : ["-k"];
        return await ExtendedConformanceHarness.RunAsync(InProcessCurl.RunAsync, [route], [.. trust, .. arguments], timeProvider: clock).WaitAsync(TimeSpan.FromSeconds(60));
    }

    private static string Text(byte[] bytes) => Encoding.UTF8.GetString(bytes).ReplaceLineEndings("\n");
}
