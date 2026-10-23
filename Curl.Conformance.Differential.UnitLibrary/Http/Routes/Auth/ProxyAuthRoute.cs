using System.Runtime.CompilerServices;
using Curl.Conformance.Differential.Http.Core;

namespace Curl.Conformance.Differential.Http.Routes.Auth;

/// <summary>
/// server.py's <c>/proxyauth</c>: 407 with a Basic proxy challenge for realm <c>p</c> and body <c>x</c>,
/// whatever the request; the connection is kept.
/// </summary>
public sealed class ProxyAuthRoute : IDifferentialRoute
{
    /// <inheritdoc/>
    public DifferentialRouteRank Rank => DifferentialRouteRank.Specific;

    /// <inheritdoc/>
    public bool Matches(DifferentialHttpRequest request) => request.Path == "/proxyauth";

    /// <inheritdoc/>
    public async Task<bool> RespondAsync(DifferentialRouteContext context, CancellationToken cancellationToken)
    {
        await DifferentialHttpResponseWriter.WriteAsync(
            context.Connection,
            new DifferentialHttpResponse(407, [("Proxy-Authenticate", "Basic realm=\"p\"")], "x"u8.ToArray()),
            cancellationToken);
        return false;
    }

    [ModuleInitializer]
    internal static void Register() => DifferentialRoutes.Register(new ProxyAuthRoute());
}
