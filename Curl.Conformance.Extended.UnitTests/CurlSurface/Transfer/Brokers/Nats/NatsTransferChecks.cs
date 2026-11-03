using System.Security.Cryptography.X509Certificates;
using System.Text;
using Curl.Conformance.Extended.Harness;
using Curl.Conformance.Extended.UnitTests.Nats;
using Curl.Console;

namespace Curl.Conformance.Extended.UnitTests.CurlSurface.Transfer.Brokers.Nats;

/// <summary>
/// curl's generic transfer options checked for <c>nats://</c> (plain TCP) or <c>natss://</c> (TLS from the first byte),
/// each against curl's manual page for curl 8.22.0 (curl.se/docs/manpage.html: "-v", "-o", "-O", "-w", "--fail",
/// "--fail-with-body", "-m" and "EXIT CODES") and ADR-1065, run in process against a scripted NATS server on port 4222
/// and a clock that moves only when a check advances it. The transfer is a subscription to <c>greet</c> that receives
/// one message and ends when the server closes (ADR-1065).
/// </summary>
internal static class NatsTransferChecks
{
    /// <summary>Every variable the -w check asks for, '|' between them.</summary>
    public const string WriteOutFormat = "%{exitcode}|%{errormsg}|%{url}|%{url_effective}|%{scheme}|%{remote_ip}|%{remote_port}|%{size_download}|%{time_total}\n";

    /// <summary>What the subscription writes: the message's payload and a newline (ADR-1065).</summary>
    public const string Body = "hello\n";

    /// <summary>The server's <c>-ERR</c> text, which curl writes after <c>NATS server error: </c>.</summary>
    public const string ServerErrorText = "Permissions Violation for Subscription to greet";

    private const int MaxTimeSeconds = 100;

    private static readonly KnownFailures Known = KnownFailures.Load(Path.Combine(AppContext.BaseDirectory, "KnownFailures.txt"));

    /// <summary>
    /// The line curl's TLS backend writes under -v once the handshake is done: the Schannel build on Windows, the
    /// OpenSSL build elsewhere (the lane rules, "Decisions").
    /// </summary>
    private static string TlsLine => OperatingSystem.IsWindows() ? "* schannel: " : "* SSL connection using ";

    /// <summary>-v: "Makes curl verbose during the operation"; its lines are on standard error and the output is unchanged.</summary>
    public static Task VerboseAsync(string scheme, string requirement) =>
        RunCaseAsync(requirement, async directory =>
        {
            CurlRunResult result = await RunAnsweredAsync(scheme, ["-sS", "-v"]);

            string error = Text(result.StandardError);
            Assert.AreEqual(0, result.ExitCode, error);
            Assert.AreEqual(Body, Text(result.StandardOutput), "manual, -v: nothing extra on standard output");
            StringAssert.Contains(error, $"* Established connection to localhost (127.0.0.1 port {NatsRun.Port}) from ", "manual, -v: curl 8.22.0's connection line");
            if (scheme == "natss")
            {
                StringAssert.Contains(error, TlsLine, "manual, -v: the TLS lines");
            }
        });

    /// <summary>-o: "Write output to the given file instead of stdout".</summary>
    public static Task OutputAsync(string scheme, string requirement) =>
        RunCaseAsync(requirement, async directory =>
        {
            string file = Path.Combine(directory, "out.txt");

            CurlRunResult result = await RunAnsweredAsync(scheme, ["-sS", "-o", file]);

            Assert.AreEqual(0, result.ExitCode, Text(result.StandardError));
            Assert.AreEqual(string.Empty, Text(result.StandardOutput), "manual, -o: nothing on standard output");
            Assert.AreEqual(Body, await File.ReadAllTextAsync(file), "manual, -o: the body in the file");
        });

    /// <summary>-O: "Write output to a local file named like the remote file", the URL path's last segment, here under --output-dir.</summary>
    public static Task RemoteNameAsync(string scheme, string requirement) =>
        RunCaseAsync(requirement, async directory =>
        {
            CurlRunResult result = await RunAnsweredAsync(scheme, ["-sS", "--output-dir", directory, "-O"]);

            Assert.AreEqual(0, result.ExitCode, Text(result.StandardError));
            Assert.AreEqual(string.Empty, Text(result.StandardOutput), "manual, -O: nothing on standard output");
            Assert.AreEqual(Body, await File.ReadAllTextAsync(Path.Combine(directory, "greet")), "manual, -O: the body in the file named after the URL");
        });

    /// <summary>-w: each variable as the manual defines it, after the body; %{time_total} is 0.000000 on a clock that never moves.</summary>
    public static Task WriteOutAsync(string scheme, string requirement) =>
        RunCaseAsync(requirement, async directory =>
        {
            CurlRunResult result = await RunAnsweredAsync(scheme, ["-sS", "-w", WriteOutFormat]);

            Assert.AreEqual(0, result.ExitCode, Text(result.StandardError));
            string expected = $"0||{Url(scheme)}|{Url(scheme)}|{scheme}|127.0.0.1|{NatsRun.Port}|{Encoding.UTF8.GetByteCount(Body)}|0.000000\n";
            Assert.AreEqual(Body + expected, Text(result.StandardOutput), "manual, -w");
        });

    /// <summary>
    /// --fail and --fail-with-body concern HTTP response codes (manual, "--fail"), and ADR-1065 maps no NATS server error
    /// to exit 22: a <c>-ERR</c> after SUB ends the transfer with exit 8 <c>NATS server error: &lt;text&gt;</c> with or
    /// without them.
    /// </summary>
    public static Task FailAsync(string scheme, string requirement, string option) =>
        RunCaseAsync(requirement, async directory =>
        {
            ScriptStep[] steps = [ScriptStep.Send("INFO {}\r\n"), ScriptStep.ReceiveUntil("PING\r\n"), ScriptStep.Send($"-ERR '{ServerErrorText}'\r\n")];

            CurlRunResult result = await RunAsync(scheme, new ScriptedReferenceServer(steps), ["-sS", option, "-w", "%{exitcode}:%{errormsg}", Url(scheme)], new NatsTransferClock());

            string message = $"NATS server error: {ServerErrorText}";
            Assert.AreEqual(8, result.ExitCode, Text(result.StandardError));
            Assert.AreEqual($"curl: (8) {message}\n", Text(result.StandardError), "ADR-1065: any -ERR but an authorization violation is exit 8");
            Assert.AreEqual($"8:{message}", Text(result.StandardOutput), "manual, -w %{exitcode} and %{errormsg}");
        });

    /// <summary>-m: a server that never answers curl's handshake ends the transfer with exit 28 at the deadline on the injected clock (manual, "EXIT CODES" 28).</summary>
    public static Task MaxTimeAsync(string scheme, string requirement) =>
        RunCaseAsync(requirement, async directory =>
        {
            NatsTransferClock clock = new();
            NatsInfoThenSilentServer server = new();

            Task<CurlRunResult> run = RunAsync(scheme, server, ["-sS", "-m", $"{MaxTimeSeconds}", Url(scheme)], clock);
            await server.Reached.WaitAsync(TimeSpan.FromSeconds(30));
            for (int second = 0; second <= MaxTimeSeconds && !run.IsCompleted; second++)
            {
                clock.Advance(TimeSpan.FromSeconds(1));
                await Task.Yield();
            }

            CurlRunResult result = await run.WaitAsync(TimeSpan.FromSeconds(30));
            string error = Text(result.StandardError);
            Assert.AreEqual(28, result.ExitCode, error);
            StringAssert.StartsWith(error, "curl: (28) ", "manual, EXIT CODES 28");
            Assert.AreEqual(string.Empty, Text(result.StandardOutput));
        });

    private static string Url(string scheme) => $"{scheme}://localhost:{NatsRun.Port}/greet";

    private static Task RunCaseAsync(string requirement, Func<string, Task> body) =>
        Known.RunCaseAsync(
            [requirement],
            async () =>
            {
                string directory = Directory.CreateTempSubdirectory("nats-transfer-").FullName;
                try
                {
                    await body(directory);
                }
                finally
                {
                    Directory.Delete(directory, recursive: true);
                }
            });

    private static Task<CurlRunResult> RunAnsweredAsync(string scheme, string[] options)
    {
        ScriptStep[] steps = [ScriptStep.Send("INFO {}\r\n"), ScriptStep.ReceiveUntil("PING\r\n"), ScriptStep.Send("PONG\r\nMSG greet 1 5\r\nhello\r\n")];
        return RunAsync(scheme, new ScriptedReferenceServer(steps), [.. options, Url(scheme)], new NatsTransferClock());
    }

    private static async Task<CurlRunResult> RunAsync(string scheme, IReferenceServer server, string[] arguments, TimeProvider clock)
    {
        using X509Certificate2? certificate = scheme == "natss" ? ReferenceCertificate.CreateSelfSigned("localhost") : null;
        ReferenceRoute route = certificate is null ? ReferenceRoute.Tcp(NatsRun.Port, server) : ReferenceRoute.Tls(NatsRun.Port, server, certificate);
        string[] trust = certificate is null ? [] : ["-k"];
        return await ExtendedConformanceHarness.RunAsync(InProcessCurl.RunAsync, [route], [.. trust, .. arguments], timeProvider: clock).WaitAsync(TimeSpan.FromSeconds(60));
    }

    private static string Text(byte[] bytes) => Encoding.UTF8.GetString(bytes).ReplaceLineEndings("\n");
}
