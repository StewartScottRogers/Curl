using Curl.Conformance.Extended.Coap;
using Curl.Conformance.Extended.Harness;
using Curl.Console;

namespace Curl.Conformance.Extended.UnitTests.Coap.Messaging;

/// <summary>
/// Runs <c>curl -sS</c> against a <see cref="CoapReferenceServer" /> on port 5683 under a <see cref="TimerJumpingClock" />:
/// once curl has sent its first datagram, whenever a timer is pending the clock jumps to it, so retransmission
/// timeouts pass at once and each datagram is stamped with the exact time curl's timer gave it.
/// </summary>
internal static class CoapMessagingRun
{
    public const string Url = "coap://localhost/hello";

    private static readonly KnownFailures KnownFailures = KnownFailures.Load(Path.Combine(AppContext.BaseDirectory, "KnownFailures.txt"));

    public static Task CaseAsync(IReadOnlyList<string> requirementIds, Func<Task> runCase) => KnownFailures.RunCaseAsync(requirementIds, runCase);

    public static async Task<CurlRunResult> RunAsync(CoapReferenceServer server, TimerJumpingClock clock, CancellationToken cancellationToken, params string[] arguments)
    {
        Task<CurlRunResult> run = ExtendedConformanceHarness.RunAsync(
            InProcessCurl.RunAsync, [ReferenceRoute.Udp(5683, server)], ["-sS", .. arguments], timeProvider: clock);
        long deadline = Environment.TickCount64 + 30_000;
        while (!run.IsCompleted && Environment.TickCount64 < deadline)
        {
            if (server.Received.Count == 0 || !clock.JumpToNextTimer())
            {
                await Task.Delay(1, cancellationToken);
            }
        }

        return await run.WaitAsync(TimeSpan.FromSeconds(30), cancellationToken);
    }

    public static CoapReferenceServer Server(TimerJumpingClock clock, CoapResponder respond) => new(respond, clock);
}
