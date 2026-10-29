using Curl.Extension.Abstractions.Arguments;

namespace Curl.Extension.OpenApi;

/// <summary>Makes the failure an OpenAPI expansion ends the run with (ADR-1075).</summary>
internal static class OpenApiFailure
{
    /// <summary>Gives a failure printed as <c>curl: openapi: </c> and the message, with exit code 2, as curl gives for a command line it cannot use.</summary>
    /// <param name="message">What is wrong.</param>
    /// <returns>The exception to throw.</returns>
    internal static ArgumentExpansionException Of(string message) => new($"openapi: {message}", CurlExitCode.FailedInit);
}
