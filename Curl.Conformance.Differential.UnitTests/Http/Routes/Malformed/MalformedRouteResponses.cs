using Curl.Conformance.Differential.Http.Core;
using Curl.Conformance.Differential.UnitTests.Http.Core;

namespace Curl.Conformance.Differential.UnitTests.Http.Routes.Malformed;

/// <summary>Sends one route the same <c>GET</c> twice and returns what it wrote, so a test sees whether the connection closed after the first.</summary>
internal static class MalformedRouteResponses
{
    internal static Task<string> RespondTwiceAsync(IDifferentialRoute route, string path) =>
        DifferentialHttpResponderTests.RespondAsync(
            [route], new DifferentialResponderState(), $"GET {path} HTTP/1.1\r\n\r\nGET {path} HTTP/1.1\r\n\r\n");
}
