using System.Runtime.CompilerServices;
using Curl.Conformance.Differential.Http.Core;

namespace Curl.Conformance.Differential.Http.Routes.Auth;

/// <summary>
/// server.py's <c>/retry</c>: counts its requests in the per-run counter <c>retry</c>; the first two get 503
/// with <c>Retry-After: 1</c> and <c>later\n</c>, every later one <c>finally\n</c>. The connection is kept.
/// </summary>
public sealed class RetryRoute : IDifferentialRoute
{
    /// <inheritdoc/>
    public DifferentialRouteRank Rank => DifferentialRouteRank.Specific;

    /// <inheritdoc/>
    public bool Matches(DifferentialHttpRequest request) => request.Path == "/retry";

    /// <inheritdoc/>
    public async Task<bool> RespondAsync(DifferentialRouteContext context, CancellationToken cancellationToken)
    {
        var response = context.State.IncrementCounter("retry") < 3
            ? new DifferentialHttpResponse(503, [("Retry-After", "1")], "later\n"u8.ToArray())
            : new DifferentialHttpResponse(200, [], "finally\n"u8.ToArray());
        await DifferentialHttpResponseWriter.WriteAsync(context.Connection, response, cancellationToken);
        return false;
    }

    [ModuleInitializer]
    internal static void Register() => DifferentialRoutes.Register(new RetryRoute());
}
