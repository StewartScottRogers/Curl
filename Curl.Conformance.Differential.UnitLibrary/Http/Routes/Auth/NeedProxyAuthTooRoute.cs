using System.Runtime.CompilerServices;
using Curl.Conformance.Differential.Http.Core;

namespace Curl.Conformance.Differential.Http.Routes.Auth;

/// <summary>
/// server.py's <c>/need-proxy-auth-too</c>: 407 with a Basic proxy challenge for realm <c>p</c> and body
/// <c>x</c>, whatever the request; the connection is kept.
/// </summary>
public sealed class NeedProxyAuthTooRoute : IDifferentialRoute
{
    /// <inheritdoc/>
    public DifferentialRouteRank Rank => DifferentialRouteRank.Specific;

    /// <inheritdoc/>
    public bool Matches(DifferentialHttpRequest request) => request.Path == "/need-proxy-auth-too";

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
    internal static void Register() => DifferentialRoutes.Register(new NeedProxyAuthTooRoute());
}
