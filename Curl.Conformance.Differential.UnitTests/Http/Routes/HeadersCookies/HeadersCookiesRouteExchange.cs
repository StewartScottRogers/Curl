using Curl.Conformance.Differential.Http.Core;
using Curl.Conformance.Differential.UnitTests.Http.Core;

namespace Curl.Conformance.Differential.UnitTests.Http.Routes.HeadersCookies;

/// <summary>Sends <c>GET</c> requests for the given paths, one after another on one connection, to a single route and returns what it wrote.</summary>
internal static class HeadersCookiesRouteExchange
{
    /// <summary>The <c>Date</c> header line every <c>resp</c> answer carries.</summary>
    internal const string Date = "Date: Thu, 01 Jan 2026 00:00:00 GMT\r\n";

    internal static Task<string> RespondAsync(IDifferentialRoute route, params string[] paths) =>
        DifferentialHttpResponderTests.RespondAsync(
            [route], new DifferentialResponderState(), string.Concat(paths.Select(path => $"GET {path} HTTP/1.1\r\n\r\n")));
}
