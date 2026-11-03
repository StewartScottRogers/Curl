using System.Security.Cryptography.X509Certificates;
using System.Text;
using Curl.Conformance.Extended.Harness;
using Curl.Conformance.Extended.Kafka;
using Curl.Conformance.Extended.Kafka.Records;
using Curl.Console;

namespace Curl.Conformance.Extended.UnitTests.CurlSurface.Transfer.Brokers.Kafka;

/// <summary>
/// curl's generic transfer options checked for <c>kafka://</c> or <c>kafkas://</c>, each against curl's manual page for
/// curl 8.22.0 (curl.se/docs/manpage.html: "-v", "-o", "-O", "-w", "--fail", "--fail-with-body", "-m" and "EXIT
/// CODES") and the extension's ADRs (ADR-1076, ADR-1080, ADR-1218), run in process through the harness against
/// <see cref="KafkaReferenceBroker" /> on a clock that moves only when a check advances it.
/// </summary>
/// <param name="scheme">The URL scheme, <c>kafka</c> or <c>kafkas</c>.</param>
/// <param name="port">The port the URL names, the broker's metadata advertises and the route serves.</param>
/// <param name="idNumber">The number that ends this scheme's requirement IDs: 1 for <c>kafka</c>, 2 for <c>kafkas</c>.</param>
internal sealed class KafkaTransferChecks(string scheme, int port, int idNumber)
{
    /// <summary>Every variable the -w check asks for, '|' between them.</summary>
    public const string WriteOutFormat = "%{exitcode}|%{errormsg}|%{url}|%{url_effective}|%{scheme}|%{remote_ip}|%{remote_port}|%{size_download}|%{time_total}\n";

    /// <summary>What consuming the topic's partition 0 writes: each record value and a line feed (ADR-1076).</summary>
    public const string Body = "a\nb\n";

    private const string Topic = "orders";

    private const int MaxTimeSeconds = 100;

    private static readonly KnownFailures Known = KnownFailures.Load(Path.Combine(AppContext.BaseDirectory, "KnownFailures.txt"));

    /// <summary>Gets the URL every check transfers: partition 0 of <see cref="Topic" />, whose last segment <c>-O</c> names the file after.</summary>
    public string Url => $"{scheme}://localhost:{port}/{Topic}/0";

    private bool IsTls => scheme == "kafkas";

    /// <summary>
    /// The line curl's TLS backend writes under -v once the handshake is done: the Schannel build on Windows, the
    /// OpenSSL build elsewhere (the lane rules, "Decisions").
    /// </summary>
    private static string TlsLine => OperatingSystem.IsWindows() ? "* schannel: " : "* SSL connection using ";

    /// <summary>The message the failing broker's OFFSET_OUT_OF_RANGE ends the transfer with (exit 8, ADR-1076).</summary>
    private static string FailureMessage => $"Kafka broker answered error 1 (OFFSET_OUT_OF_RANGE) for topic {Topic} partition 0";

    /// <summary>The requirement ID of <paramref name="option" /> for this scheme, <c>CurlSurface:Kafka&lt;option&gt;-&lt;n&gt;</c>.</summary>
    public string Requirement(string option) => $"CurlSurface:Kafka{option}-{idNumber}";

    /// <summary>-v: "Makes curl verbose during the operation"; its lines are on standard error and the output is unchanged.</summary>
    public Task VerboseAsync() =>
        RunCaseAsync("Verbose", async directory =>
        {
            CurlRunResult result = await RunAnsweredAsync(["-sS", "-v", Url]);

            string error = Text(result.StandardError);
            Assert.AreEqual(0, result.ExitCode, error);
            Assert.AreEqual(Body, Text(result.StandardOutput), "manual, -v: nothing extra on standard output");
            StringAssert.Contains(error, $"* Established connection to localhost (127.0.0.1 port {port}) from ", "manual, -v: curl 8.22.0's connection line");
            if (IsTls)
            {
                StringAssert.Contains(error, TlsLine, "manual, -v: the TLS lines");
            }
        });

    /// <summary>-o: "Write output to the given file instead of stdout".</summary>
    public Task OutputAsync() =>
        RunCaseAsync("Output", async directory =>
        {
            string file = Path.Combine(directory, "out.txt");

            CurlRunResult result = await RunAnsweredAsync(["-sS", "-o", file, Url]);

            Assert.AreEqual(0, result.ExitCode, Text(result.StandardError));
            Assert.AreEqual(string.Empty, Text(result.StandardOutput), "manual, -o: nothing on standard output");
            Assert.AreEqual(Body, await File.ReadAllTextAsync(file), "manual, -o: the body in the file");
        });

    /// <summary>-O: "Write output to a local file named like the remote file", the URL path's last segment <c>0</c>, here under --output-dir.</summary>
    public Task RemoteNameAsync() =>
        RunCaseAsync("RemoteName", async directory =>
        {
            CurlRunResult result = await RunAnsweredAsync(["-sS", "--output-dir", directory, "-O", Url]);

            Assert.AreEqual(0, result.ExitCode, Text(result.StandardError));
            Assert.AreEqual(string.Empty, Text(result.StandardOutput), "manual, -O: nothing on standard output");
            Assert.AreEqual(Body, await File.ReadAllTextAsync(Path.Combine(directory, "0")), "manual, -O: the body in the file named after the URL");
        });

    /// <summary>
    /// -w: each variable as the manual defines it, after the body; %{time_total} is 0.000000 on a clock that never moves
    /// and %{size_download} the bytes written.
    /// </summary>
    public Task WriteOutAsync() =>
        RunCaseAsync("WriteOut", async directory =>
        {
            CurlRunResult result = await RunAnsweredAsync(["-sS", "-w", WriteOutFormat, Url]);

            Assert.AreEqual(0, result.ExitCode, Text(result.StandardError));
            string expected = $"0||{Url}|{Url}|{scheme}|127.0.0.1|{port}|{Encoding.UTF8.GetByteCount(Body)}|0.000000\n";
            Assert.AreEqual(Body + expected, Text(result.StandardOutput), "manual, -w");
        });

    /// <summary>
    /// --fail and --fail-with-body: the extension maps no broker error to exit 22, so a broker's OFFSET_OUT_OF_RANGE ends
    /// the transfer with exit 8 and its message under either option, as without it (ADR-1076, ADR-1218; manual,
    /// "--fail": HTTP response codes only, "EXIT CODES" 8).
    /// </summary>
    public Task FailAsync(string option, string requirementOption) =>
        RunCaseAsync(requirementOption, async directory =>
        {
            CurlRunResult result = await RunAsync(
                Broker(new KafkaFetchApi(Log())),
                ["-sS", option, "-w", "%{exitcode}:%{errormsg}", Url + "?offset=10"],
                new KafkaTransferClock());

            Assert.AreEqual(8, result.ExitCode, Text(result.StandardError));
            Assert.AreEqual($"curl: (8) {FailureMessage}\n", Text(result.StandardError), "ADR-1218; manual, EXIT CODES 8");
            Assert.AreEqual($"8:{FailureMessage}", Text(result.StandardOutput), "manual, -w %{exitcode} and %{errormsg}");
        });

    /// <summary>-m: a broker that never answers ends the transfer with exit 28 at the deadline on the injected clock (manual, "EXIT CODES" 28).</summary>
    public Task MaxTimeAsync() =>
        RunCaseAsync("MaxTime", async directory =>
        {
            KafkaTransferClock clock = new();
            NeverAnsweringBroker server = new();

            Task<CurlRunResult> run = RunAsync(server, ["-sS", "-m", $"{MaxTimeSeconds}", Url], clock);
            await server.Reached.WaitAsync(TimeSpan.FromSeconds(30));
            // Until the run ends, not for -m + 1 seconds: curl may reach a clock timer only after the deadline (BL-4663).
            CurlRunResult result = await MaxTimeDeadlineRun.AdvanceUntilDoneAsync(run, clock.Advance);
            string error = Text(result.StandardError);
            Assert.AreEqual(28, result.ExitCode, error);
            StringAssert.StartsWith(error, "curl: (28) ", "manual, EXIT CODES 28");
            Assert.AreEqual(string.Empty, Text(result.StandardOutput));
        });

    private static KafkaReferenceLog Log()
    {
        KafkaReferenceLog log = new();
        log.Append(KafkaReferenceRecordBatch.Write(0, [KafkaReferenceRecord.Plain(0, "a"), KafkaReferenceRecord.Plain(1, "b")]));
        return log;
    }

    private static string Text(byte[] bytes) => Encoding.UTF8.GetString(bytes).ReplaceLineEndings("\n");

    private Task RunCaseAsync(string option, Func<string, Task> body) =>
        Known.RunCaseAsync(
            [Requirement(option)],
            async () =>
            {
                string directory = Directory.CreateTempSubdirectory("kafka-transfer-").FullName;
                try
                {
                    await body(directory);
                }
                finally
                {
                    Directory.Delete(directory, recursive: true);
                }
            });

    // One broker that leads partition 0 itself, so the transfer stays on the URL's connection (ADR-1080).
    private KafkaReferenceBroker Broker(KafkaFetchApi fetch) =>
        new(new KafkaMetadataApi([new KafkaReferenceNode(1, "localhost", port)], [new KafkaReferenceTopic(Topic, 0, [new KafkaReferencePartition(0, 0, 1)])]), fetch);

    private Task<CurlRunResult> RunAnsweredAsync(string[] arguments) => RunAsync(Broker(new KafkaFetchApi(Log())), arguments, new KafkaTransferClock());

    private async Task<CurlRunResult> RunAsync(IReferenceServer server, string[] arguments, TimeProvider clock)
    {
        using X509Certificate2? certificate = IsTls ? ReferenceCertificate.CreateSelfSigned("localhost") : null;
        ReferenceRoute route = IsTls ? ReferenceRoute.Tls(port, server, certificate!) : ReferenceRoute.Tcp(port, server);
        string[] trust = IsTls ? ["-k"] : [];
        return await ExtendedConformanceHarness.RunAsync(InProcessCurl.RunAsync, [route], [.. trust, .. arguments], timeProvider: clock).WaitAsync(TimeSpan.FromSeconds(60));
    }

    /// <summary>A broker that reads what curl sends and never sends a byte; <see cref="Reached" /> completes at curl's first byte.</summary>
    private sealed class NeverAnsweringBroker : IReferenceServer
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
