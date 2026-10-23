using System.Runtime.CompilerServices;
using Curl.Conformance.Differential.Http.Core;

namespace Curl.Conformance.Differential.Http.Routes.Auth;

/// <summary>
/// server.py's <c>/retry500</c>: counts its requests in the per-run counter <c>r5</c>; the first gets 500 with
/// <c>err\n</c>, every later one <c>ok after 500\n</c>. The connection is kept.
/// </summary>
public sealed class Retry500Route : IDifferentialRoute
{
    /// <inheritdoc/>
    public DifferentialRouteRank Rank => DifferentialRouteRank.Specific;

    /// <inheritdoc/>
    public bool Matches(DifferentialHttpRequest request) => request.Path == "/retry500";

    /// <inheritdoc/>
    public async Task<bool> RespondAsync(DifferentialRouteContext context, CancellationToken cancellationToken)
    {
        var response = context.State.IncrementCounter("r5") < 2
            ? new DifferentialHttpResponse(500, [], "err\n"u8.ToArray())
            : new DifferentialHttpResponse(200, [], "ok after 500\n"u8.ToArray());
        await DifferentialHttpResponseWriter.WriteAsync(context.Connection, response, cancellationToken);
        return false;
    }

    [ModuleInitializer]
    internal static void Register() => DifferentialRoutes.Register(new Retry500Route());
}
