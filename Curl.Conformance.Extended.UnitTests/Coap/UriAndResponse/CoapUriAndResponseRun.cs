using System.Text;
using Curl.Conformance.Extended.Coap;
using Curl.Conformance.Extended.Harness;
using Curl.Conformance.Extended.UnitTests.Coap.Messaging;
using Curl.Console;

namespace Curl.Conformance.Extended.UnitTests.Coap.UriAndResponse;

/// <summary>
/// Runs <c>curl -sS</c> against a <see cref="CoapReferenceServer" /> that answers each request with one piggybacked
/// response, on the UDP port the case names, under a <see cref="TimerJumpingClock" />.
/// </summary>
internal static class CoapUriAndResponseRun
{
    /// <summary>The run's outcome and the datagrams curl sent.</summary>
    /// <param name="Result">What curl wrote and its exit code.</param>
    /// <param name="Sent">The datagrams the server received.</param>
    public sealed record Outcome(CurlRunResult Result, IReadOnlyList<CoapReceivedDatagram> Sent)
    {
        /// <summary>Gets stdout as UTF-8.</summary>
        public string StandardOutput => Encoding.UTF8.GetString(Result.StandardOutput);

        /// <summary>Gets stderr as UTF-8.</summary>
        public string StandardError => Encoding.UTF8.GetString(Result.StandardError);

        /// <summary>Gets the bytes of the one request after its header and token: its options and payload.</summary>
        public byte[] OptionBytes
        {
            get
            {
                CoapReceivedDatagram request = Sent.Single();
                return request.Bytes[(4 + request.Message.Token.Length)..];
            }
        }
    }

    public static async Task<Outcome> RunAsync(int port, byte responseCode, string responsePayload, CancellationToken cancellationToken, params string[] arguments)
    {
        TimerJumpingClock clock = new();
        CoapReferenceServer server = new((request, _) => [CoapResponses.Piggybacked(request, responseCode, Encoding.UTF8.GetBytes(responsePayload))], clock);
        Task<CurlRunResult> run = ExtendedConformanceHarness.RunAsync(
            InProcessCurl.RunAsync, [ReferenceRoute.Udp(port, server)], ["-sS", .. arguments], timeProvider: clock);
        long deadline = Environment.TickCount64 + 30_000;
        while (!run.IsCompleted && Environment.TickCount64 < deadline)
        {
            if (server.Received.Count == 0 || !clock.JumpToNextTimer())
            {
                await Task.Delay(1, cancellationToken);
            }
        }

        CurlRunResult result = await run.WaitAsync(TimeSpan.FromSeconds(30), cancellationToken);
        return new Outcome(result, server.Received);
    }
}
