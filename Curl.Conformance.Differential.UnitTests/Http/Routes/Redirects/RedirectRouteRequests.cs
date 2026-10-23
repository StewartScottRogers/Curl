using Curl.Conformance.Differential.Http.Core;
using Curl.Conformance.Differential.UnitTests.Http.Core;

namespace Curl.Conformance.Differential.UnitTests.Http.Routes.Redirects;

/// <summary>Sends a redirect route its path twice, then a path it does not match, over one in-memory connection.</summary>
internal static class RedirectRouteRequests
{
    /// <summary>The <c>Date</c> header line every answer carries.</summary>
    public const string Date = "Date: Thu, 01 Jan 2026 00:00:00 GMT\r\n";

    /// <summary>
    /// The bytes <paramref name="route"/> writes, read as Latin-1, for two requests to <paramref name="path"/> and
    /// one to <c>/other</c>: both answers when the route keeps the connection, and nothing for <c>/other</c>,
    /// which no route answers, so the connection ends there.
    /// </summary>
    public static Task<string> RespondTwiceThenToOtherPathAsync(IDifferentialRoute route, string path) =>
        DifferentialHttpResponderTests.RespondAsync(
            [route],
            new DifferentialResponderState(),
            $"GET {path} HTTP/1.1\r\n\r\nPOST {path}?q=1 HTTP/1.1\r\nContent-Length: 0\r\n\r\nGET /other HTTP/1.1\r\n\r\n");
}
