using Curl.Conformance.Extended.Coap;
using Curl.Conformance.Extended.Coap.Dtls;
using Curl.Conformance.Extended.Harness;
using Curl.Conformance.Extended.UnitTests.Coap.Messaging;
using Curl.Console;

namespace Curl.Conformance.Extended.UnitTests.Coap.Dtls;

/// <summary>
/// Runs <c>curl -sS</c> for <c>coaps://localhost/hello</c> against a <see cref="DtlsReferenceServer" /> on port 5684
/// in front of a <see cref="CoapReferenceServer" />. The clock never moves: the server answers each flight at once,
/// so no retransmission timer is due and every datagram is sent exactly once.
/// </summary>
internal static class CoapDtlsRun
{
    public const string Url = "coaps://localhost/hello";

    public const int Port = 5684;

    public static Task<CurlRunResult> RunAsync(DtlsReferenceServer server, params string[] arguments) =>
        ExtendedConformanceHarness.RunAsync(InProcessCurl.RunAsync, [ReferenceRoute.Udp(Port, server)], ["-sS", .. arguments, Url], timeProvider: new TimerJumpingClock());

    /// <summary>Runs as <see cref="RunAsync" /> does, but jumps the clock to the next pending timer whenever <paramref name="curlIsWaitingInVain" /> says so.</summary>
    public static async Task<CurlRunResult> RunJumpingAsync(DtlsReferenceServer server, Func<bool> curlIsWaitingInVain, params string[] arguments)
    {
        TimerJumpingClock clock = new();
        Task<CurlRunResult> run = ExtendedConformanceHarness.RunAsync(
            InProcessCurl.RunAsync, [ReferenceRoute.Udp(Port, server)], ["-sS", .. arguments, Url], timeProvider: clock);
        long deadline = Environment.TickCount64 + 30_000;
        while (!run.IsCompleted && Environment.TickCount64 < deadline)
        {
            if (!curlIsWaitingInVain() || !clock.JumpToNextTimer())
            {
                await Task.Delay(1);
            }
        }

        return await run.WaitAsync(TimeSpan.FromSeconds(30));
    }

    public static CoapReferenceServer CoapServer(CoapResponder respond) => new(respond, new TimerJumpingClock());
}
