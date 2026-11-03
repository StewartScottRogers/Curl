using System.Security.Cryptography.X509Certificates;
using System.Text;
using Curl.Conformance.Extended.Amqp0;
using Curl.Conformance.Extended.Amqp0.Messaging;
using Curl.Conformance.Extended.Harness;
using Curl.Conformance.Extended.UnitTests.CurlSurface.Transfer.Realtime;
using Curl.Console;

namespace Curl.Conformance.Extended.UnitTests.CurlSurface.Transfer.Brokers.Amqp0;

/// <summary>
/// curl's generic transfer options checked for <c>amqp://</c> or <c>amqps://</c>, each against curl's manual page for
/// curl 8.22.0 (curl.se/docs/manpage.html: "-v", "-o", "-O", "-w", "--fail", "--fail-with-body", "-m" and "EXIT CODES")
/// and the extension's ADRs (ADR-1060, ADR-1091), run in process against the reference broker on a clock that moves
/// only when a check advances it. The answered transfer is a basic.get of one message from <c>/vh/q</c>.
/// </summary>
internal static class Amqp0TransferChecks
{
    /// <summary>The message the answering broker hands to basic.get.</summary>
    public const string Body = "amqp message\n";

    /// <summary>Every variable the -w check asks for, '|' between them.</summary>
    public const string WriteOutFormat = "%{exitcode}|%{errormsg}|%{url}|%{url_effective}|%{scheme}|%{remote_ip}|%{remote_port}|%{size_download}|%{time_total}\n";

    /// <summary>The channel.close text the failing broker answers queue.declare with.</summary>
    public const string FailureText = "NOT_FOUND - no queue 'q' in vhost 'vh'";

    /// <summary>ADR-1060: a channel.close other than 403 ends the transfer with exit 8 and this message, --fail or not.</summary>
    public const string FailureMessage = $"AMQP server sent channel.close: reply code 404, {FailureText}";

    private const int MaxTimeSeconds = 100;

    private static readonly KnownFailures Known = KnownFailures.Load(Path.Combine(AppContext.BaseDirectory, "KnownFailures.txt"));

    /// <summary>The line curl's TLS backend writes under -v: the Schannel build on Windows, the OpenSSL build elsewhere.</summary>
    private static string TlsLine => OperatingSystem.IsWindows() ? "* schannel: " : "* SSL connection using ";

    /// <summary>-v: its lines are on standard error and the output is unchanged (manual, "-v").</summary>
    public static Task VerboseAsync(string scheme) =>
        RunCaseAsync(scheme, "Verbose", async directory =>
        {
            CurlRunResult result = await RunAnsweredAsync(scheme, ["-sS", "-v"]);

            string error = Text(result.StandardError);
            Assert.AreEqual(0, result.ExitCode, error);
            Assert.AreEqual(Body, Text(result.StandardOutput), "manual, -v: nothing extra on standard output");
            StringAssert.Contains(error, $"* Established connection to localhost (127.0.0.1 port {Port(scheme)}) from ", "manual, -v: curl 8.22.0's connection line");
            if (IsTls(scheme))
            {
                StringAssert.Contains(error, TlsLine, "manual, -v: the TLS lines");
            }
        });

    /// <summary>-o: "Write output to the given file instead of stdout".</summary>
    public static Task OutputAsync(string scheme) =>
        RunCaseAsync(scheme, "Output", async directory =>
        {
            string file = Path.Combine(directory, "out.txt");

            CurlRunResult result = await RunAnsweredAsync(scheme, ["-sS", "-o", file]);

            Assert.AreEqual(0, result.ExitCode, Text(result.StandardError));
            Assert.AreEqual(string.Empty, Text(result.StandardOutput), "manual, -o: nothing on standard output");
            Assert.AreEqual(Body, await File.ReadAllTextAsync(file), "manual, -o: the body in the file");
        });

    /// <summary>-O: the file is named after the URL path's last segment, the queue <c>q</c>, here under --output-dir (manual, "-O").</summary>
    public static Task RemoteNameAsync(string scheme) =>
        RunCaseAsync(scheme, "RemoteName", async directory =>
        {
            CurlRunResult result = await RunAnsweredAsync(scheme, ["-sS", "--output-dir", directory, "-O"]);

            Assert.AreEqual(0, result.ExitCode, Text(result.StandardError));
            Assert.AreEqual(string.Empty, Text(result.StandardOutput), "manual, -O: nothing on standard output");
            Assert.AreEqual(Body, await File.ReadAllTextAsync(Path.Combine(directory, "q")), "manual, -O: the body in the file named after the URL");
        });

    /// <summary>-w: each variable as the manual defines it, after the body; %{time_total} is 0.000000 on a clock that never moves.</summary>
    public static Task WriteOutAsync(string scheme) =>
        RunCaseAsync(scheme, "WriteOut", async directory =>
        {
            CurlRunResult result = await RunAnsweredAsync(scheme, ["-sS", "-w", WriteOutFormat]);

            Assert.AreEqual(0, result.ExitCode, Text(result.StandardError));
            string expected = $"0||{Url(scheme)}|{Url(scheme)}|{scheme}|127.0.0.1|{Port(scheme)}|{Encoding.UTF8.GetByteCount(Body)}|0.000000\n";
            Assert.AreEqual(Body + expected, Text(result.StandardOutput), "manual, -w");
        });

    /// <summary>
    /// --fail and --fail-with-body concern HTTP response codes (manual, "--fail"); ADR-1060 maps a broker's channel.close
    /// to exit 8, so the transfer ends with exit 8 and the close's reply code and text with either option, never 22.
    /// </summary>
    public static Task FailAsync(string scheme, string option, string check) =>
        RunCaseAsync(scheme, check, async directory =>
        {
            Amqp0ReferenceBroker broker = new(new Amqp0BrokerSettings(), Amqp0MethodTable.Standard.WithChannelClosedOnDeclare(404, FailureText));

            CurlRunResult result = await RunAsync(scheme, broker, ["-sS", option, "-w", "%{exitcode}:%{errormsg}", Url(scheme)], new RealtimeTransferClock());

            Assert.AreEqual(8, result.ExitCode, Text(result.StandardError));
            Assert.AreEqual($"curl: (8) {FailureMessage}\n", Text(result.StandardError), "ADR-1060; manual, EXIT CODES 8");
            Assert.AreEqual($"8:{FailureMessage}", Text(result.StandardOutput), "manual, -w %{exitcode} and %{errormsg}");
        });

    /// <summary>-m: a broker that never answers ends the transfer with exit 28 at the deadline on the injected clock (manual, "-m", "EXIT CODES" 28).</summary>
    public static Task MaxTimeAsync(string scheme) =>
        RunCaseAsync(scheme, "MaxTime", async directory =>
        {
            RealtimeTransferClock clock = new();
            NeverAnsweringServer server = new();

            Task<CurlRunResult> run = RunAsync(scheme, server, ["-sS", "-m", $"{MaxTimeSeconds}", Url(scheme)], clock);
            await server.Reached.WaitAsync(TimeSpan.FromSeconds(30));
            // Until the run ends, not for -m + 1 seconds: curl may reach a clock timer only after the deadline (BL-4663).
            CurlRunResult result = await MaxTimeDeadlineRun.AdvanceUntilDoneAsync(run, clock.Advance);
            string error = Text(result.StandardError);
            Assert.AreEqual(28, result.ExitCode, error);
            StringAssert.StartsWith(error, "curl: (28) ", "manual, EXIT CODES 28");
            Assert.AreEqual(string.Empty, Text(result.StandardOutput));
        });

    /// <summary>The requirement ID of <paramref name="check" />: <c>-1</c> for amqp, <c>-2</c> for amqps.</summary>
    private static string Requirement(string scheme, string check) => $"CurlSurface:Amqp0{check}-{(IsTls(scheme) ? 2 : 1)}";

    private static bool IsTls(string scheme) => scheme == "amqps";

    private static int Port(string scheme) => IsTls(scheme) ? 5671 : 5672;

    private static string Url(string scheme) => $"{scheme}://localhost:{Port(scheme)}/vh/q";

    private static Task RunCaseAsync(string scheme, string check, Func<string, Task> body) =>
        Known.RunCaseAsync(
            [Requirement(scheme, check)],
            async () =>
            {
                string directory = Directory.CreateTempSubdirectory("amqp0-transfer-").FullName;
                try
                {
                    await body(directory);
                }
                finally
                {
                    Directory.Delete(directory, recursive: true);
                }
            });

    private static async Task<CurlRunResult> RunAnsweredAsync(string scheme, string[] options)
    {
        Amqp0ReferenceBroker broker = new(new Amqp0BrokerSettings(), Amqp0MethodTable.Standard.WithGetOk(Encoding.UTF8.GetBytes(Body), 4000));
        return await RunAsync(scheme, broker, [.. options, Url(scheme)], new RealtimeTransferClock());
    }

    private static async Task<CurlRunResult> RunAsync(string scheme, IReferenceServer server, string[] arguments, TimeProvider clock)
    {
        using X509Certificate2? certificate = IsTls(scheme) ? ReferenceCertificate.CreateSelfSigned("localhost") : null;
        ReferenceRoute route = IsTls(scheme) ? ReferenceRoute.Tls(Port(scheme), server, certificate!) : ReferenceRoute.Tcp(Port(scheme), server);
        string[] trust = IsTls(scheme) ? ["-k"] : [];
        return await ExtendedConformanceHarness.RunAsync(InProcessCurl.RunAsync, [route], [.. trust, .. arguments], timeProvider: clock).WaitAsync(TimeSpan.FromSeconds(60));
    }

    private static string Text(byte[] bytes) => Encoding.UTF8.GetString(bytes).ReplaceLineEndings("\n");
}
