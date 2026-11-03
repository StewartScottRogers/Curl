using System.Security.Cryptography.X509Certificates;
using System.Text;
using Curl.Conformance.Extended.Harness;
using Curl.Conformance.Extended.UnitTests.CurlSurface.Transfer.Realtime;
using Curl.Conformance.Extended.UnitTests.Stomp;
using Curl.Console;

namespace Curl.Conformance.Extended.UnitTests.CurlSurface.Transfer.Brokers.Stomp;

/// <summary>
/// curl's generic transfer options checked for <c>stomp://</c> or <c>stomps://</c>, each against curl's manual page for
/// curl 8.22.0 (curl.se/docs/manpage.html: "-v", "-o", "-O", "-w", "--fail", "--fail-with-body", "-m" and "EXIT CODES")
/// and ADR-1059, run in process against a <see cref="ScriptedReferenceServer" /> on a clock that moves only when a check
/// advances it. The answered transfer subscribes to <c>/q</c> and writes the one MESSAGE's body; the failed one gets an
/// ERROR frame, which ADR-1059 ends with exit 8 whether or not <c>--fail</c> is given.
/// </summary>
internal static class StompTransferChecks
{
    /// <summary>Every variable the -w check asks for, '|' between them.</summary>
    public const string WriteOutFormat = "%{exitcode}|%{errormsg}|%{url}|%{url_effective}|%{scheme}|%{remote_ip}|%{remote_port}|%{size_download}|%{time_total}\n";

    /// <summary>What the answered transfer writes: the body of the one MESSAGE frame.</summary>
    public const string Body = "hi\n";

    /// <summary>The message ADR-1059 gives the failed transfer's ERROR frame.</summary>
    public const string FailureMessage = "STOMP server sent ERROR: no such queue";

    private const int MaxTimeSeconds = 100;

    private static readonly KnownFailures Known = KnownFailures.Load(Path.Combine(AppContext.BaseDirectory, "KnownFailures.txt"));

    /// <summary>
    /// The line curl's TLS backend writes under -v once the handshake is done: the Schannel build on Windows, the
    /// OpenSSL build elsewhere (the lane rules, "Decisions").
    /// </summary>
    private static string TlsLine => OperatingSystem.IsWindows() ? "* schannel: " : "* SSL connection using ";

    /// <summary>-v: "Makes curl verbose during the operation"; its lines are on standard error and the output is unchanged.</summary>
    public static Task VerboseAsync(StompTransferScheme scheme) =>
        RunCaseAsync(scheme, "Verbose", async directory =>
        {
            CurlRunResult result = await RunAnsweredAsync(scheme, ["-sS", "-v"]);

            string error = Text(result.StandardError);
            Assert.AreEqual(0, result.ExitCode, error);
            Assert.AreEqual(Body, Text(result.StandardOutput), "manual, -v: nothing extra on standard output");
            StringAssert.Contains(error, $"* Established connection to localhost (127.0.0.1 port {scheme.Port}) from ", "manual, -v: curl 8.22.0's connection line");
            if (scheme.Tls)
            {
                StringAssert.Contains(error, TlsLine, "manual, -v: the TLS lines");
            }
        });

    /// <summary>-o: "Write output to the given file instead of stdout".</summary>
    public static Task OutputAsync(StompTransferScheme scheme) =>
        RunCaseAsync(scheme, "Output", async directory =>
        {
            string file = Path.Combine(directory, "out.txt");

            CurlRunResult result = await RunAnsweredAsync(scheme, ["-sS", "-o", file]);

            Assert.AreEqual(0, result.ExitCode, Text(result.StandardError));
            Assert.AreEqual(string.Empty, Text(result.StandardOutput), "manual, -o: nothing on standard output");
            Assert.AreEqual(Body, await File.ReadAllTextAsync(file), "manual, -o: the body in the file");
        });

    /// <summary>-O: "Write output to a local file named like the remote file", the URL path's last segment <c>q</c>, here under --output-dir.</summary>
    public static Task RemoteNameAsync(StompTransferScheme scheme) =>
        RunCaseAsync(scheme, "RemoteName", async directory =>
        {
            CurlRunResult result = await RunAnsweredAsync(scheme, ["-sS", "--output-dir", directory, "-O"]);

            Assert.AreEqual(0, result.ExitCode, Text(result.StandardError));
            Assert.AreEqual(string.Empty, Text(result.StandardOutput), "manual, -O: nothing on standard output");
            Assert.AreEqual(Body, await File.ReadAllTextAsync(Path.Combine(directory, "q")), "manual, -O: the body in the file named after the URL");
        });

    /// <summary>
    /// -w: each variable as the manual defines it, after the body; %{time_total} is 0.000000 on a clock that never moves
    /// and %{size_download} the bytes written.
    /// </summary>
    public static Task WriteOutAsync(StompTransferScheme scheme) =>
        RunCaseAsync(scheme, "WriteOut", async directory =>
        {
            CurlRunResult result = await RunAnsweredAsync(scheme, ["-sS", "-w", WriteOutFormat]);

            Assert.AreEqual(0, result.ExitCode, Text(result.StandardError));
            string expected = $"0||{scheme.Url}|{scheme.Url}|{scheme.Scheme}|127.0.0.1|{scheme.Port}|{Encoding.UTF8.GetByteCount(Body)}|0.000000\n";
            Assert.AreEqual(Body + expected, Text(result.StandardOutput), "manual, -w");
        });

    /// <summary>
    /// --fail and --fail-with-body concern HTTP response codes (manual, "--fail"); STOMP has none, and ADR-1059 ends an
    /// ERROR frame with exit 8 and its message with or without them, which %{exitcode} and %{errormsg} agree with.
    /// </summary>
    public static Task FailAsync(StompTransferScheme scheme, string option, string check) =>
        RunCaseAsync(scheme, check, async directory =>
        {
            ScriptStep[] steps = StompRun.SubscribeAnswered(StompRun.Connected, "ERROR\nmessage:no such queue\n\n\0");

            CurlRunResult result = await RunAsync(scheme, new ScriptedReferenceServer(steps), ["-sS", option, "-w", "%{exitcode}:%{errormsg}", scheme.Url], new RealtimeTransferClock());

            Assert.AreEqual(8, result.ExitCode, Text(result.StandardError));
            Assert.AreEqual($"curl: (8) {FailureMessage}\n", Text(result.StandardError), "ADR-1059: an ERROR frame is exit 8");
            Assert.AreEqual($"8:{FailureMessage}", Text(result.StandardOutput), "manual, -w %{exitcode} and %{errormsg}");
        });

    /// <summary>-m: a server that never answers CONNECT ends the transfer with exit 28 at the deadline on the injected clock (manual, "EXIT CODES" 28).</summary>
    public static Task MaxTimeAsync(StompTransferScheme scheme) =>
        RunCaseAsync(scheme, "MaxTime", async directory =>
        {
            RealtimeTransferClock clock = new();
            NeverAnsweringServer server = new();

            Task<CurlRunResult> run = RunAsync(scheme, server, ["-sS", "-m", $"{MaxTimeSeconds}", scheme.Url], clock);
            await server.Reached.WaitAsync(TimeSpan.FromSeconds(30));
            // Until the run ends, not for -m + 1 seconds: curl may reach a clock timer only after the deadline (BL-4663).
            CurlRunResult result = await MaxTimeDeadlineRun.AdvanceUntilDoneAsync(run, clock.Advance);
            string error = Text(result.StandardError);
            Assert.AreEqual(28, result.ExitCode, error);
            StringAssert.StartsWith(error, "curl: (28) ", "manual, EXIT CODES 28");
            Assert.AreEqual(string.Empty, Text(result.StandardOutput));
        });

    private static Task RunCaseAsync(StompTransferScheme scheme, string check, Func<string, Task> body) =>
        Known.RunCaseAsync(
            [scheme.Requirement(check)],
            async () =>
            {
                string directory = Directory.CreateTempSubdirectory("stomp-transfer-").FullName;
                try
                {
                    await body(directory);
                }
                finally
                {
                    Directory.Delete(directory, recursive: true);
                }
            });

    private static Task<CurlRunResult> RunAnsweredAsync(StompTransferScheme scheme, string[] options)
    {
        ScriptStep[] steps = StompRun.SubscribeAnswered(StompRun.Connected, $"MESSAGE\nsubscription:0\nmessage-id:1\ndestination:/q\ncontent-length:{Body.Length}\n\n{Body}\0");
        return RunAsync(scheme, new ScriptedReferenceServer(steps), [.. options, scheme.Url], new RealtimeTransferClock());
    }

    private static async Task<CurlRunResult> RunAsync(StompTransferScheme scheme, IReferenceServer server, string[] arguments, TimeProvider clock)
    {
        using X509Certificate2? certificate = scheme.Tls ? ReferenceCertificate.CreateSelfSigned("localhost") : null;
        ReferenceRoute route = scheme.Tls ? ReferenceRoute.Tls(scheme.Port, server, certificate!) : ReferenceRoute.Tcp(scheme.Port, server);
        string[] trust = scheme.Tls ? ["-k"] : [];
        return await ExtendedConformanceHarness.RunAsync(InProcessCurl.RunAsync, [route], [.. trust, .. arguments], timeProvider: clock).WaitAsync(TimeSpan.FromSeconds(60));
    }

    private static string Text(byte[] bytes) => Encoding.UTF8.GetString(bytes).ReplaceLineEndings("\n");
}

/// <summary>One STOMP scheme as the transfer-surface checks drive it.</summary>
/// <param name="Scheme">The URL scheme, <c>stomp</c> or <c>stomps</c>.</param>
/// <param name="Port">The port the URL names and the route serves: 61613 or 61614.</param>
/// <param name="Tls">Whether the route serves the connection over TLS.</param>
/// <param name="Number">The number that ends the scheme's requirement IDs: 1 for <c>stomp</c>, 2 for <c>stomps</c>.</param>
internal sealed record StompTransferScheme(string Scheme, int Port, bool Tls, int Number)
{
    public static StompTransferScheme Stomp { get; } = new("stomp", StompRun.Port, false, 1);

    public static StompTransferScheme Stomps { get; } = new("stomps", StompRun.SecurePort, true, 2);

    public string Url => $"{Scheme}://localhost:{Port}/q";

    /// <summary>The requirement ID of <paramref name="check" /> for this scheme, <c>CurlSurface:Stomp&lt;check&gt;-&lt;n&gt;</c>.</summary>
    public string Requirement(string check) => $"CurlSurface:Stomp{check}-{Number}";
}
