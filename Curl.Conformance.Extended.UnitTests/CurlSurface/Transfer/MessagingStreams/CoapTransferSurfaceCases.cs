using System.Globalization;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Curl.Conformance.Extended.Coap;
using Curl.Conformance.Extended.Coap.Dtls;
using Curl.Conformance.Extended.Harness;
using Curl.Conformance.Extended.UnitTests.Coap.Messaging;
using Curl.Console;
using static Curl.Conformance.Extended.UnitTests.CurlSurface.Transfer.MessagingStreams.TransferSurfaceCaseRun;

namespace Curl.Conformance.Extended.UnitTests.CurlSurface.Transfer.MessagingStreams;

/// <summary>
/// curl's generic transfer options for <c>coap://</c> (UDP port 5683) and <c>coaps://</c> (DTLS 1.2 on UDP port 5684,
/// curl run with <c>-k</c> against a fresh self-signed certificate): each check sends <c>GET</c> for
/// <c>coap://localhost/hello</c> to the CoAP reference server, which answers with a piggybacked 2.05 Content of
/// <c>hello</c> or a 4.04 Not Found of <c>missing</c>, and pins exit code, standard output and standard error. Expected
/// values come from curl 8.22.0's manual page and, for <c>--fail</c> on a 4.xx response, from the CoAP library's
/// response-code mapping (RFC 7252 section 5.9: a 4.xx or 5.xx response is exit 22 under <c>--fail</c>, as an HTTP
/// error status is).
/// </summary>
[TestClass]
public sealed class CoapTransferSurfaceCases
{
    private const string Reply = "hello";

    private const string ErrorReply = "missing";

    private const string ErrorMessage = "The requested URL returned error: 4.04";

    private const byte NotFound = 0x84;

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [SpecRequirement("CurlSurface:Verbose-Coap-1")]
    [SpecRequirement("CurlSurface:Verbose-Coaps-1")]
    [DataRow("coap", "CurlSurface:Verbose-Coap-1")]
    [DataRow("coaps", "CurlSurface:Verbose-Coaps-1")]
    public Task Verbose_SuccessfulGet_WritesConnectionLinesToStandardErrorAndOnlyTheReplyToStandardOutput(string scheme, string requirement) =>
        Known.RunCaseAsync(
            [requirement],
            async () =>
            {
                CurlRunResult result = await RunAsync(scheme, CoapResponses.Content, Reply, "-v");

                AssertVerboseRun(result, requirement, "manual: -v", Reply, VerboseLines(scheme));
            });

    [TestMethod]
    [SpecRequirement("CurlSurface:Output-Coap-1")]
    [SpecRequirement("CurlSurface:Output-Coaps-1")]
    [DataRow("coap", "CurlSurface:Output-Coap-1")]
    [DataRow("coaps", "CurlSurface:Output-Coaps-1")]
    public Task Output_File_WritesTheReplyToTheFileAndNothingToStandardOutput(string scheme, string requirement) =>
        Known.RunCaseAsync(
            [requirement],
            async () =>
            {
                string directory = CreateTemporaryDirectory();
                try
                {
                    string file = Path.Combine(directory, "reply.txt");

                    CurlRunResult result = await RunAsync(scheme, CoapResponses.Content, Reply, "-o", file);

                    AssertRun(result, requirement, "manual: -o", 0, string.Empty, string.Empty);
                    Assert.AreEqual(Reply, File.ReadAllText(file), $"{requirement}: file");
                }
                finally
                {
                    Directory.Delete(directory, true);
                }
            });

    [TestMethod]
    [SpecRequirement("CurlSurface:RemoteName-Coap-1")]
    [SpecRequirement("CurlSurface:RemoteName-Coaps-1")]
    [DataRow("coap", "CurlSurface:RemoteName-Coap-1")]
    [DataRow("coaps", "CurlSurface:RemoteName-Coaps-1")]
    public Task RemoteName_WritesTheReplyToAFileNamedForTheUrlsLastSegment(string scheme, string requirement) =>
        Known.RunCaseAsync(
            [requirement],
            async () =>
            {
                string directory = CreateTemporaryDirectory();
                try
                {
                    CurlRunResult result = await RunAsync(scheme, CoapResponses.Content, Reply, "--output-dir", directory, "-O");

                    AssertRun(result, requirement, "manual: -O", 0, string.Empty, string.Empty);
                    Assert.AreEqual(Reply, File.ReadAllText(Path.Combine(directory, "hello")), $"{requirement}: file");
                }
                finally
                {
                    Directory.Delete(directory, true);
                }
            });

    [TestMethod]
    [SpecRequirement("CurlSurface:WriteOut-Coap-1")]
    [SpecRequirement("CurlSurface:WriteOut-Coaps-1")]
    [DataRow("coap", "CurlSurface:WriteOut-Coap-1")]
    [DataRow("coaps", "CurlSurface:WriteOut-Coaps-1")]
    public Task WriteOut_SuccessfulGet_WritesEveryVariableAfterTheReply(string scheme, string requirement) =>
        Known.RunCaseAsync(
            [requirement],
            async () =>
            {
                CurlRunResult result = await RunAsync(scheme, CoapResponses.Content, Reply, "-w", WriteOutFormat);

                AssertRun(result, requirement, "manual: -w", 0, $"{Reply}0||{Url(scheme)}|{Url(scheme)}|{scheme}|127.0.0.1|{Port(scheme)}|{Reply.Length}|0.000000", string.Empty);
            });

    [TestMethod]
    [SpecRequirement("CurlSurface:WriteOut-Coap-2")]
    [SpecRequirement("CurlSurface:WriteOut-Coaps-2")]
    [DataRow("coap", "CurlSurface:WriteOut-Coap-2")]
    [DataRow("coaps", "CurlSurface:WriteOut-Coaps-2")]
    public Task WriteOut_NotFoundUnderFail_WritesExitCode22AndTheErrorMessage(string scheme, string requirement) =>
        Known.RunCaseAsync(
            [requirement],
            async () =>
            {
                CurlRunResult result = await RunAsync(scheme, NotFound, ErrorReply, "--fail", "-w", WriteOutFormat);

                AssertRun(
                    result,
                    requirement,
                    "manual: -w, --fail; RFC 7252 section 5.9: 4.04 is an error response",
                    22,
                    $"22|{ErrorMessage}|{Url(scheme)}|{Url(scheme)}|{scheme}|127.0.0.1|{Port(scheme)}|0|0.000000",
                    $"curl: (22) {ErrorMessage}\n");
            });

    [TestMethod]
    [SpecRequirement("CurlSurface:Fail-Coap-1")]
    [SpecRequirement("CurlSurface:Fail-Coaps-1")]
    [DataRow("coap", "CurlSurface:Fail-Coap-1")]
    [DataRow("coaps", "CurlSurface:Fail-Coaps-1")]
    public Task Fail_NotFound_Exits22AndWritesNothing(string scheme, string requirement) =>
        Known.RunCaseAsync(
            [requirement],
            async () =>
            {
                CurlRunResult result = await RunAsync(scheme, NotFound, ErrorReply, "--fail");

                AssertRun(result, requirement, "manual: --fail, EXIT CODES 22", 22, string.Empty, $"curl: (22) {ErrorMessage}\n");
            });

    [TestMethod]
    [SpecRequirement("CurlSurface:FailWithBody-Coap-1")]
    [SpecRequirement("CurlSurface:FailWithBody-Coaps-1")]
    [DataRow("coap", "CurlSurface:FailWithBody-Coap-1")]
    [DataRow("coaps", "CurlSurface:FailWithBody-Coaps-1")]
    public Task FailWithBody_NotFound_Exits22AndWritesTheBody(string scheme, string requirement) =>
        Known.RunCaseAsync(
            [requirement],
            async () =>
            {
                CurlRunResult result = await RunAsync(scheme, NotFound, ErrorReply, "--fail-with-body");

                AssertRun(result, requirement, "manual: --fail-with-body, EXIT CODES 22", 22, ErrorReply, $"curl: (22) {ErrorMessage}\n");
            });

    [TestMethod]
    [SpecRequirement("CurlSurface:Fail-Coap-2")]
    [SpecRequirement("CurlSurface:Fail-Coaps-2")]
    [DataRow("coap", "CurlSurface:Fail-Coap-2")]
    [DataRow("coaps", "CurlSurface:Fail-Coaps-2")]
    public Task Fail_SuccessfulGet_WritesTheReplyAndExits0(string scheme, string requirement) =>
        Known.RunCaseAsync(
            [requirement],
            async () =>
            {
                CurlRunResult result = await RunAsync(scheme, CoapResponses.Content, Reply, "--fail");

                AssertRun(result, requirement, "manual: --fail", 0, Reply, string.Empty);
            });

    [TestMethod]
    [SpecRequirement("CurlSurface:MaxTime-Coap-1")]
    [SpecRequirement("CurlSurface:MaxTime-Coaps-1")]
    [DataRow("coap", "CurlSurface:MaxTime-Coap-1")]
    [DataRow("coaps", "CurlSurface:MaxTime-Coaps-1")]
    public Task MaxTime_ServerNeverAnswers_Exits28AtTheDeadline(string scheme, string requirement) =>
        Known.RunCaseAsync(
            [requirement],
            async () =>
            {
                CurlRunResult result = await RunPastMaxTimeAsync(scheme, 5, TestContext.CancellationToken);

                AssertRun(result, requirement, "manual: -m, EXIT CODES 28; ADR-1222", 28, string.Empty, TimeoutMessage(scheme));
            });

    private static string Url(string scheme) => $"{scheme}://localhost/hello";

    private static int Port(string scheme) => scheme == "coaps" ? 5684 : 5683;

    // A timeout before the DTLS handshake finishes is in curl's connect phase (ADR-1222).
    private static string TimeoutMessage(string scheme) => scheme == "coaps"
        ? "curl: (28) Connection timed out after 5000 milliseconds\n"
        : "curl: (28) Operation timed out after 5000 milliseconds with 0 bytes received\n";

    // The connection line every transfer's -v writes (curl 8.22.0's "* Established connection to localhost (127.0.0.1
    // port N) from ...", as the xmpp cases measured), and for coaps the line curl's OpenSSL build writes for a DTLS
    // session on every platform, since the DTLS is hand-built everywhere (ADR-1222).
    private static string[] VerboseLines(string scheme) => scheme == "coaps"
        ? [$"* Established connection to localhost (127.0.0.1 port {Port(scheme)}) from ", "* SSL connection using DTLSv1.2"]
        : [$"* Established connection to localhost (127.0.0.1 port {Port(scheme)}) from "];

    /// <summary>Runs curl with <paramref name="arguments" /> for the scheme's URL against a server answering every request with one piggybacked response.</summary>
    private static async Task<CurlRunResult> RunAsync(string scheme, byte code, string payload, params string[] arguments)
    {
        TimerJumpingClock clock = new();
        CoapReferenceServer coap = new((request, _) => [CoapResponses.Piggybacked(request, code, Encoding.UTF8.GetBytes(payload))], clock);
        if (scheme != "coaps")
        {
            return await ExtendedConformanceHarness.RunAsync(InProcessCurl.RunAsync, [ReferenceRoute.Udp(5683, coap)], ["-sS", .. arguments, Url(scheme)], timeProvider: clock);
        }

        using X509Certificate2 certificate = ReferenceCertificate.CreateSelfSigned("localhost");
        using DtlsReferenceServer dtls = new(coap, certificate);
        return await ExtendedConformanceHarness.RunAsync(InProcessCurl.RunAsync, [ReferenceRoute.Udp(5684, dtls)], ["-sS", "-k", .. arguments, Url(scheme)], timeProvider: clock);
    }

    /// <summary>
    /// Runs curl with <c>-m <paramref name="seconds" /></c> against a server that reads every datagram and never answers,
    /// moving the clock from timer to timer once the first datagram arrives, until curl gives up.
    /// </summary>
    private static async Task<CurlRunResult> RunPastMaxTimeAsync(string scheme, int seconds, CancellationToken cancellationToken)
    {
        TimerJumpingClock clock = new();
        SilentDatagramServer server = new();
        string[] leading = scheme == "coaps" ? ["-k"] : [];
        Task<CurlRunResult> run = ExtendedConformanceHarness.RunAsync(
            InProcessCurl.RunAsync,
            [ReferenceRoute.Udp(Port(scheme), server)],
            ["-sS", .. leading, "-m", seconds.ToString(CultureInfo.InvariantCulture), Url(scheme)],
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
