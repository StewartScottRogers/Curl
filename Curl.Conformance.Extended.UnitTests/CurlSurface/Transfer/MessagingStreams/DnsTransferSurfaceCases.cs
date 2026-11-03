using System.Buffers.Binary;
using System.Globalization;
using System.Text;
using Curl.Conformance.Extended.Harness;
using Curl.Conformance.Extended.UnitTests.Coap.Messaging;
using Curl.Console;
using static Curl.Conformance.Extended.UnitTests.CurlSurface.Transfer.MessagingStreams.TransferSurfaceCaseRun;

namespace Curl.Conformance.Extended.UnitTests.CurlSurface.Transfer.MessagingStreams;

/// <summary>
/// curl's generic transfer options for <c>dns://</c> (UDP port 53, the default <c>--dns-transport</c>): each check asks
/// <c>dns://localhost/example.com</c> for its A record of a datagram server in this file, which copies the query's
/// message ID and question and answers <c>192.0.2.1</c> (NOERROR) or nothing (NXDOMAIN), and pins exit code, standard
/// output and standard error. Expected values come from curl 8.22.0's manual page and, for the answer's text and for
/// <c>--fail</c>, from ADR-1140: the answer is written as dig-like text and any response code, NXDOMAIN included, ends
/// with exit 0, as dig does, so <c>--fail</c> never turns a DNS answer into exit 22.
/// </summary>
[TestClass]
public sealed class DnsTransferSurfaceCases
{
    private const string Url = "dns://localhost/example.com";

    private const ushort NoError = 0x8180;

    private const ushort NameError = 0x8183;

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [SpecRequirement("CurlSurface:Verbose-Dns-1")]
    public Task Verbose_SuccessfulQuery_WritesTheConnectionLineToStandardErrorAndOnlyTheAnswerToStandardOutput() =>
        Known.RunCaseAsync(
            ["CurlSurface:Verbose-Dns-1"],
            async () =>
            {
                DnsAnswerServer server = new(NoError);

                CurlRunResult result = await RunAsync(server, "-v");

                AssertVerboseRun(result, "CurlSurface:Verbose-Dns-1", "manual: -v", server.ExpectedText(), "* Established connection to localhost (127.0.0.1 port 53) from ");
            });

    [TestMethod]
    [SpecRequirement("CurlSurface:Output-Dns-1")]
    public Task Output_File_WritesTheAnswerToTheFileAndNothingToStandardOutput() =>
        Known.RunCaseAsync(
            ["CurlSurface:Output-Dns-1"],
            async () =>
            {
                string directory = CreateTemporaryDirectory();
                try
                {
                    string file = Path.Combine(directory, "answer.txt");
                    DnsAnswerServer server = new(NoError);

                    CurlRunResult result = await RunAsync(server, "-o", file);

                    AssertRun(result, "CurlSurface:Output-Dns-1", "manual: -o", 0, string.Empty, string.Empty);
                    Assert.AreEqual(server.ExpectedText(), File.ReadAllText(file), "CurlSurface:Output-Dns-1: file");
                }
                finally
                {
                    Directory.Delete(directory, true);
                }
            });

    [TestMethod]
    [SpecRequirement("CurlSurface:RemoteName-Dns-1")]
    public Task RemoteName_WritesTheAnswerToAFileNamedForTheUrlsLastSegment() =>
        Known.RunCaseAsync(
            ["CurlSurface:RemoteName-Dns-1"],
            async () =>
            {
                string directory = CreateTemporaryDirectory();
                try
                {
                    DnsAnswerServer server = new(NoError);

                    CurlRunResult result = await RunAsync(server, "--output-dir", directory, "-O");

                    AssertRun(result, "CurlSurface:RemoteName-Dns-1", "manual: -O", 0, string.Empty, string.Empty);
                    Assert.AreEqual(server.ExpectedText(), File.ReadAllText(Path.Combine(directory, "example.com")), "CurlSurface:RemoteName-Dns-1: file");
                }
                finally
                {
                    Directory.Delete(directory, true);
                }
            });

    [TestMethod]
    [SpecRequirement("CurlSurface:WriteOut-Dns-1")]
    public Task WriteOut_SuccessfulQuery_WritesEveryVariableAfterTheAnswer() =>
        Known.RunCaseAsync(
            ["CurlSurface:WriteOut-Dns-1"],
            async () =>
            {
                DnsAnswerServer server = new(NoError);

                CurlRunResult result = await RunAsync(server, "-w", WriteOutFormat);

                string answer = server.ExpectedText();
                AssertRun(result, "CurlSurface:WriteOut-Dns-1", "manual: -w", 0, $"{answer}0||{Url}|{Url}|dns|127.0.0.1|53|{Encoding.UTF8.GetByteCount(answer)}|0.000000", string.Empty);
            });

    [TestMethod]
    [SpecRequirement("CurlSurface:WriteOut-Dns-2")]
    public Task WriteOut_NameErrorUnderFail_WritesExitCode0AsDnsMapsNoResponseCodeTo22() =>
        Known.RunCaseAsync(
            ["CurlSurface:WriteOut-Dns-2"],
            async () =>
            {
                DnsAnswerServer server = new(NameError);

                CurlRunResult result = await RunAsync(server, "--fail", "-w", WriteOutFormat);

                string answer = server.ExpectedText();
                AssertRun(
                    result,
                    "CurlSurface:WriteOut-Dns-2",
                    "manual: -w, --fail; ADR-1140: any response code ends with exit 0",
                    0,
                    $"{answer}0||{Url}|{Url}|dns|127.0.0.1|53|{Encoding.UTF8.GetByteCount(answer)}|0.000000",
                    string.Empty);
            });

    [TestMethod]
    [SpecRequirement("CurlSurface:Fail-Dns-1")]
    [SpecRequirement("CurlSurface:FailWithBody-Dns-1")]
    [DataRow("--fail", "CurlSurface:Fail-Dns-1")]
    [DataRow("--fail-with-body", "CurlSurface:FailWithBody-Dns-1")]
    public Task Fail_NameError_StaysExit0AndWritesTheAnswerAsDnsMapsNoResponseCodeTo22(string option, string requirement) =>
        Known.RunCaseAsync(
            [requirement],
            async () =>
            {
                DnsAnswerServer server = new(NameError);

                CurlRunResult result = await RunAsync(server, option);

                AssertRun(result, requirement, $"manual: {option}; ADR-1140: NXDOMAIN is written and ends with exit 0, as dig does", 0, server.ExpectedText(), string.Empty);
            });

    [TestMethod]
    [SpecRequirement("CurlSurface:Fail-Dns-2")]
    public Task Fail_SuccessfulQuery_WritesTheAnswerAndExits0() =>
        Known.RunCaseAsync(
            ["CurlSurface:Fail-Dns-2"],
            async () =>
            {
                DnsAnswerServer server = new(NoError);

                CurlRunResult result = await RunAsync(server, "--fail");

                AssertRun(result, "CurlSurface:Fail-Dns-2", "manual: --fail", 0, server.ExpectedText(), string.Empty);
            });

    [TestMethod]
    [SpecRequirement("CurlSurface:MaxTime-Dns-1")]
    public Task MaxTime_ServerNeverAnswers_Exits28AtTheDeadline() =>
        Known.RunCaseAsync(
            ["CurlSurface:MaxTime-Dns-1"],
            async () =>
            {
                CurlRunResult result = await RunPastMaxTimeAsync(2, TestContext.CancellationToken);

                AssertRun(result, "CurlSurface:MaxTime-Dns-1", "manual: -m, EXIT CODES 28", 28, string.Empty, "curl: (28) Operation timed out after 2000 milliseconds with 0 bytes received\n");
            });

    /// <summary>Runs curl with <paramref name="arguments" /> for the URL against <paramref name="server" /> on UDP port 53.</summary>
    private static Task<CurlRunResult> RunAsync(DnsAnswerServer server, params string[] arguments) =>
        TransferSurfaceCaseRun.RunAsync(TransferSurfaceRoute.Udp(53, () => server), ["-sS", .. arguments, Url]);

    /// <summary>
    /// Runs curl with <c>-m <paramref name="seconds" /></c> against a server that reads every datagram and never answers,
    /// moving the clock from timer to timer once the first datagram arrives, until curl gives up. The deadline is shorter
    /// than the UDP transport's 5-second wait for an answer, so <c>-m</c> ends the run, not the transport's own retries.
    /// </summary>
    private static async Task<CurlRunResult> RunPastMaxTimeAsync(int seconds, CancellationToken cancellationToken)
    {
        TimerJumpingClock clock = new();
        SilentDatagramServer server = new();
        Task<CurlRunResult> run = ExtendedConformanceHarness.RunAsync(
            InProcessCurl.RunAsync,
            [ReferenceRoute.Udp(53, server)],
            ["-sS", "-m", seconds.ToString(CultureInfo.InvariantCulture), Url],
            timeProvider: clock);
        await server.FirstDatagramReceived.WaitAsync(TimeSpan.FromSeconds(30), cancellationToken);
        long deadline = Environment.TickCount64 + 30_000;
        while (!run.IsCompleted && Environment.TickCount64 < deadline)
        {
            if (!clock.JumpToNextTimer())
            {
                await Task.Delay(1, cancellationToken);
            }

            await Task.Yield();
        }

        return await run.WaitAsync(TimeSpan.FromSeconds(30), cancellationToken);
    }

    /// <summary>
    /// A DNS server (RFC 1035 section 4.1) answering each query with its message ID and question copied, the given
    /// flags, and for NOERROR one A record <c>192.0.2.1</c> with a TTL of 300.
    /// </summary>
    private sealed class DnsAnswerServer(ushort flags) : IDatagramReferenceServer
    {
        private const int HeaderLength = 12;

        private ushort? queryId;

        private bool HasAnswer => (flags & 0x000F) == 0;

        public IReadOnlyList<ReadOnlyMemory<byte>> Answer(ReadOnlyMemory<byte> datagram)
        {
            ReadOnlySpan<byte> query = datagram.Span;
            queryId = BinaryPrimitives.ReadUInt16BigEndian(query);
            byte[] header = new byte[HeaderLength];
            query[..2].CopyTo(header);
            BinaryPrimitives.WriteUInt16BigEndian(header.AsSpan(2), flags);
            BinaryPrimitives.WriteUInt16BigEndian(header.AsSpan(4), 1);
            BinaryPrimitives.WriteUInt16BigEndian(header.AsSpan(6), (ushort)(HasAnswer ? 1 : 0));
            byte[] record = HasAnswer ? [0xC0, 0x0C, 0, 1, 0, 1, 0, 0, 0x01, 0x2C, 0, 4, 192, 0, 2, 1] : [];
            return [(byte[])[.. header, .. query[HeaderLength..], .. record]];
        }

        /// <summary>The dig-like text Curl writes for this server's answer to the query it last received (ADR-1140).</summary>
        public string ExpectedText()
        {
            string status = HasAnswer ? "NOERROR" : "NXDOMAIN";
            string text =
                $";; ->>HEADER<<- opcode: QUERY, status: {status}, id: {queryId}\n" +
                $";; flags: qr rd ra; QUERY: 1, ANSWER: {(HasAnswer ? 1 : 0)}, AUTHORITY: 0, ADDITIONAL: 0\n" +
                "\n;; QUESTION SECTION:\n;example.com.\t\tIN\tA\n";
            return HasAnswer ? text + "\n;; ANSWER SECTION:\nexample.com.\t300\tIN\tA\t192.0.2.1\n" : text;
        }
    }

    /// <summary>A datagram server that reads everything curl sends and never sends a datagram.</summary>
    private sealed class SilentDatagramServer : IDatagramReferenceServer
    {
        private readonly TaskCompletionSource firstDatagramReceived = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task FirstDatagramReceived => firstDatagramReceived.Task;

        public IReadOnlyList<ReadOnlyMemory<byte>> Answer(ReadOnlyMemory<byte> datagram)
        {
            firstDatagramReceived.TrySetResult();
            return [];
        }
    }
}
