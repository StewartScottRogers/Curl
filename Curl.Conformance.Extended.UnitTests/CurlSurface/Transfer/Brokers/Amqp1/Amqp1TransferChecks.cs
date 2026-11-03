using System.Security.Cryptography.X509Certificates;
using System.Text;
using Curl.Conformance.Extended.Amqp1;
using Curl.Conformance.Extended.Amqp1.Messaging;
using Curl.Conformance.Extended.Harness;
using Curl.Console;

namespace Curl.Conformance.Extended.UnitTests.CurlSurface.Transfer.Brokers.Amqp1;

/// <summary>
/// curl's generic transfer options checked for one AMQP 1.0 scheme, each against curl's manual page for curl 8.22.0
/// (curl.se/docs/manpage.html: "-v", "-o", "-O", "-w", "--fail", "--fail-with-body", "-m" and "EXIT CODES") and the
/// extension's ADRs, receiving from <see cref="Amqp1ReferenceContainer" /> in process on a clock that moves only when a
/// check advances it.
/// </summary>
internal static class Amqp1TransferChecks
{
    /// <summary>Every variable the -w check asks for, '|' between them.</summary>
    public const string WriteOutFormat = "%{exitcode}|%{errormsg}|%{url}|%{url_effective}|%{scheme}|%{remote_ip}|%{remote_port}|%{size_download}|%{time_total}\n";

    /// <summary>The message body the container delivers to an answered receive.</summary>
    public const string Body = "hello";

    /// <summary>
    /// The error of a receive whose link the container refuses with amqp:not-found: exit 78, remote file not found
    /// (ADR-1062's mapping of a detach error, <c>Amqp1ClientMessages.PeerError</c>).
    /// </summary>
    public const string RefusedLinkMessage = $"AMQP 1.0 detach error: {Amqp1ReferenceLinkRefusal.Condition}: {Amqp1ReferenceLinkRefusal.Description}";

    private const int MaxTimeSeconds = 100;

    private static readonly KnownFailures Known = KnownFailures.Load(Path.Combine(AppContext.BaseDirectory, "KnownFailures.txt"));

    /// <summary>
    /// The line curl's TLS backend writes under -v once the handshake is done: the Schannel build on Windows, the
    /// OpenSSL build elsewhere (the lane rules, "Decisions").
    /// </summary>
    private static string TlsLine => OperatingSystem.IsWindows() ? "* schannel: " : "* SSL connection using ";

    /// <summary>-v: "Makes curl verbose during the operation"; its lines are on standard error and the output is unchanged.</summary>
    public static Task VerboseAsync(Amqp1TransferScheme scheme) =>
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
    public static Task OutputAsync(Amqp1TransferScheme scheme) =>
        RunCaseAsync(scheme, "Output", async directory =>
        {
            string file = Path.Combine(directory, "out.txt");

            CurlRunResult result = await RunAnsweredAsync(scheme, ["-sS", "-o", file]);

            Assert.AreEqual(0, result.ExitCode, Text(result.StandardError));
            Assert.AreEqual(string.Empty, Text(result.StandardOutput), "manual, -o: nothing on standard output");
            Assert.AreEqual(Body, await File.ReadAllTextAsync(file), "manual, -o: the body in the file");
        });

    /// <summary>-O: "Write output to a local file named like the remote file", the URL path's last segment, here under --output-dir.</summary>
    public static Task RemoteNameAsync(Amqp1TransferScheme scheme) =>
        RunCaseAsync(scheme, "RemoteName", async directory =>
        {
            CurlRunResult result = await RunAnsweredAsync(scheme, ["-sS", "--output-dir", directory, "-O"]);

            Assert.AreEqual(0, result.ExitCode, Text(result.StandardError));
            Assert.AreEqual(string.Empty, Text(result.StandardOutput), "manual, -O: nothing on standard output");
            Assert.AreEqual(Body, await File.ReadAllTextAsync(Path.Combine(directory, "queue")), "manual, -O: the body in the file named after the URL");
        });

    /// <summary>
    /// -w: each variable as the manual defines it, after the body; %{time_total} is 0.000000 on a clock that never moves
    /// and %{size_download} the bytes written.
    /// </summary>
    public static Task WriteOutAsync(Amqp1TransferScheme scheme) =>
        RunCaseAsync(scheme, "WriteOut", async directory =>
        {
            CurlRunResult result = await RunAnsweredAsync(scheme, ["-sS", "-w", WriteOutFormat]);

            Assert.AreEqual(0, result.ExitCode, Text(result.StandardError));
            string expected = $"0||{scheme.Url}|{scheme.Url}|{scheme.Scheme}|127.0.0.1|{scheme.Port}|{Encoding.UTF8.GetByteCount(Body)}|0.000000\n";
            Assert.AreEqual(Body + expected, Text(result.StandardOutput), "manual, -w");
        });

    /// <summary>
    /// --fail and --fail-with-body: the manual's "server errors" are HTTP response codes, and the extension maps no AMQP
    /// error to exit 22, so a refused link still ends with ADR-1062's exit 78 and its message under either option (ADR-1219).
    /// </summary>
    public static Task FailAsync(Amqp1TransferScheme scheme, string option, string requirementOption) =>
        RunCaseAsync(scheme, requirementOption, async directory =>
        {
            CurlRunResult result = await RunAsync(
                scheme,
                new Amqp1ReferenceContainer(new Amqp1ReferenceContainerOptions()),
                ["-sS", option, "-w", "%{exitcode}:%{errormsg}", scheme.Url],
                new Amqp1TransferClock());

            Assert.AreEqual(78, result.ExitCode, Text(result.StandardError));
            Assert.AreEqual($"curl: (78) {RefusedLinkMessage}\n", Text(result.StandardError), "ADR-1062: amqp:not-found is exit 78; ADR-1219");
            Assert.AreEqual($"78:{RefusedLinkMessage}", Text(result.StandardOutput), "manual, -w %{exitcode} and %{errormsg}");
        });

    /// <summary>-m: a server that never answers ends the transfer with exit 28 at the deadline on the injected clock (manual, "EXIT CODES" 28).</summary>
    public static Task MaxTimeAsync(Amqp1TransferScheme scheme) =>
        RunCaseAsync(scheme, "MaxTime", async directory =>
        {
            Amqp1TransferClock clock = new();
            NeverAnsweringServer server = new();

            Task<CurlRunResult> run = RunAsync(scheme, server, ["-sS", "-m", $"{MaxTimeSeconds}", scheme.Url], clock);
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

    private static Task RunCaseAsync(Amqp1TransferScheme scheme, string option, Func<string, Task> body) =>
        Known.RunCaseAsync(
            [scheme.Requirement(option)],
            async () =>
            {
                string directory = Directory.CreateTempSubdirectory("amqp1-transfer-").FullName;
                try
                {
                    await body(directory);
                }
                finally
                {
                    Directory.Delete(directory, recursive: true);
                }
            });

    private static Task<CurlRunResult> RunAnsweredAsync(Amqp1TransferScheme scheme, string[] options)
    {
        byte[] message = Amqp1ReferenceMessage.Encode(new Amqp1ReferenceDescribed(Amqp1ReferenceMessage.Data, Encoding.UTF8.GetBytes(Body)));
        Amqp1ReferenceContainer container = new(new Amqp1ReferenceContainerOptions
        {
            LinkBehaviour = new Amqp1ReferenceSendingLink([new(Amqp1ReferenceMessage.Transfer(0, 0, settled: false, more: false), message)]),
        });
        return RunAsync(scheme, container, [.. options, scheme.Url], new Amqp1TransferClock());
    }

    private static async Task<CurlRunResult> RunAsync(Amqp1TransferScheme scheme, IReferenceServer server, string[] arguments, TimeProvider clock)
    {
        using X509Certificate2? certificate = scheme.Tls ? ReferenceCertificate.CreateSelfSigned("localhost") : null;
        ReferenceRoute route = scheme.Tls ? ReferenceRoute.Tls(scheme.Port, server, certificate!) : ReferenceRoute.Tcp(scheme.Port, server);
        string[] trust = scheme.Tls ? ["-k"] : [];
        return await ExtendedConformanceHarness.RunAsync(InProcessCurl.RunAsync, [route], [.. trust, .. arguments], timeProvider: clock).WaitAsync(TimeSpan.FromSeconds(60));
    }

    private static string Text(byte[] bytes) => Encoding.UTF8.GetString(bytes).ReplaceLineEndings("\n");

    /// <summary>A server that reads what curl sends and never sends a byte; <see cref="Reached" /> completes at curl's first byte.</summary>
    private sealed class NeverAnsweringServer : IReferenceServer
    {
        private readonly TaskCompletionSource reached = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task Reached => reached.Task;

        public async Task ServeAsync(ReferenceConnection connection, CancellationToken cancellationToken)
        {
            byte[] buffer = new byte[4096];
            while (await connection.ReadAsync(buffer, cancellationToken) > 0)
            {
                reached.TrySetResult();
            }
        }
    }
}
