using System.Runtime.CompilerServices;
using Curl.Conformance.Differential.Http.Core;

namespace Curl.Conformance.Differential.Http.Routes.Bodies;

/// <summary>
/// server.py's <c>/slowbody</c>: sends a raw head with <c>Content-Length: 20</c> and the first 10 body bytes,
/// waits 3 seconds on the injected clock, then sends the other 10; the connection is kept.
/// </summary>
public sealed class SlowBodyRoute : IDifferentialRoute
{
    /// <summary>How long the route waits between the two halves of the body.</summary>
    public static readonly TimeSpan Delay = TimeSpan.FromSeconds(3);

    private static readonly byte[] FirstPart =
        [.. "HTTP/1.1 200 OK\r\n"u8, .. System.Text.Encoding.ASCII.GetBytes(DifferentialHttpResponseWriter.DateHeaderLine), .. "Content-Length: 20\r\n\r\n0123456789"u8];

    /// <inheritdoc/>
    public DifferentialRouteRank Rank => DifferentialRouteRank.Specific;

    /// <inheritdoc/>
    public bool Matches(DifferentialHttpRequest request) => request.Path == "/slowbody";

    /// <inheritdoc/>
    public async Task<bool> RespondAsync(DifferentialRouteContext context, CancellationToken cancellationToken)
    {
        await DifferentialHttpResponseWriter.WriteRawAsync(context.Connection, FirstPart, cancellationToken);
        await Task.Delay(Delay, context.Options.TimeProvider, cancellationToken);
        await DifferentialHttpResponseWriter.WriteRawAsync(context.Connection, "0123456789"u8.ToArray(), cancellationToken);
        return false;
    }

    [ModuleInitializer]
    internal static void Register() => DifferentialRoutes.Register(new SlowBodyRoute());
}
