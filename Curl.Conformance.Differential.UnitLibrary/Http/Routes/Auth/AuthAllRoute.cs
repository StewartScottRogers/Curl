using System.Runtime.CompilerServices;
using Curl.Conformance.Differential.Http.Core;

namespace Curl.Conformance.Differential.Http.Routes.Auth;

/// <summary>
/// server.py's <c>/authall</c>: 401 with Negotiate, NTLM, Digest and Basic challenges, in that order, and
/// body <c>x</c>, whatever the request; the connection is kept.
/// </summary>
public sealed class AuthAllRoute : IDifferentialRoute
{
    /// <inheritdoc/>
    public DifferentialRouteRank Rank => DifferentialRouteRank.Specific;

    /// <inheritdoc/>
    public bool Matches(DifferentialHttpRequest request) => request.Path == "/authall";

    /// <inheritdoc/>
    public async Task<bool> RespondAsync(DifferentialRouteContext context, CancellationToken cancellationToken)
    {
        await DifferentialHttpResponseWriter.WriteAsync(
            context.Connection,
            new DifferentialHttpResponse(
                401,
                [
                    ("WWW-Authenticate", "Negotiate"),
                    ("WWW-Authenticate", "NTLM"),
                    ("WWW-Authenticate", "Digest realm=\"r\", nonce=\"abc\", qop=\"auth\""),
                    ("WWW-Authenticate", "Basic realm=\"r\""),
                ],
                "x"u8.ToArray()),
            cancellationToken);
        return false;
    }

    [ModuleInitializer]
    internal static void Register() => DifferentialRoutes.Register(new AuthAllRoute());
}
