using Curl.Conformance.Extended.Coap;
using Curl.Conformance.Extended.Harness;
using Curl.Console;

namespace Curl.Conformance.Extended.UnitTests.Coap.BlockwiseObserve;

/// <summary>Runs <c>curl -sS</c> against a <see cref="CoapReferenceServer" /> on port 5683 on the system clock, for cases that need no timer to pass.</summary>
internal static class CoapBlockwiseObserveRun
{
    public const string Url = "coap://localhost/big";

    public const byte Changed = 0x44;

    public const byte Continue = 0x5F;

    public const byte RequestEntityIncomplete = 0x88;

    public const byte RequestEntityTooLarge = 0x8D;

    public const byte NotFound = 0x84;

    public static async Task<CurlRunResult> RunAsync(CoapReferenceServer server, CancellationToken cancellationToken, params string[] arguments) =>
        await ExtendedConformanceHarness.RunAsync(InProcessCurl.RunAsync, [ReferenceRoute.Udp(5683, server)], ["-sS", .. arguments])
            .WaitAsync(TimeSpan.FromSeconds(30), cancellationToken);

    public static CoapReferenceServer Server(CoapResponder respond) => new(respond, TimeProvider.System);

    /// <summary>A body of <paramref name="length" /> letters running a to z over and over, so a block out of place shows.</summary>
    public static byte[] Body(int length) => [.. Enumerable.Range(0, length).Select(index => (byte)('a' + (index % 26)))];
}
