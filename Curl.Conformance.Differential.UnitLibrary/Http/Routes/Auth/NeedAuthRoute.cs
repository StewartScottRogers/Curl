using System.Runtime.CompilerServices;
using Curl.Conformance.Differential.Http.Core;

namespace Curl.Conformance.Differential.Http.Routes.Auth;

/// <summary>
/// server.py's <c>/needauth</c>: <c>authed\n</c> for Basic <c>u:p</c>, otherwise 401 with a Basic challenge
/// for realm <c>r</c> and <c>denied\n</c>; the connection is kept.
/// </summary>
public sealed class NeedAuthRoute : IDifferentialRoute
{
    /// <inheritdoc/>
    public DifferentialRouteRank Rank => DifferentialRouteRank.Specific;

    /// <inheritdoc/>
    public bool Matches(DifferentialHttpRequest request) => request.Path == "/needauth";

    /// <inheritdoc/>
    public async Task<bool> RespondAsync(DifferentialRouteContext context, CancellationToken cancellationToken)
    {
        var response = context.Request.Headers.GetValueOrDefault("authorization", string.Empty) == "Basic dTpw"
            ? new DifferentialHttpResponse(200, [], "authed\n"u8.ToArray())
            : new DifferentialHttpResponse(401, [("WWW-Authenticate", "Basic realm=\"r\"")], "denied\n"u8.ToArray());
        await DifferentialHttpResponseWriter.WriteAsync(context.Connection, response, cancellationToken);
        return false;
    }

    [ModuleInitializer]
    internal static void Register() => DifferentialRoutes.Register(new NeedAuthRoute());
}
