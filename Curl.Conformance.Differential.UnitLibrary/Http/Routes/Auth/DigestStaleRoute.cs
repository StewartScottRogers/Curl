using System.Globalization;
using System.Runtime.CompilerServices;
using Curl.Conformance.Differential.Http.Core;

namespace Curl.Conformance.Differential.Http.Routes.Auth;

/// <summary>
/// server.py's <c>/digeststale</c>: counts its requests in the per-run counter <c>ds</c>; from the third
/// request on, any Digest answer gets <c>stale ok\n</c>; otherwise 401 with a Digest challenge whose nonce is
/// <c>n</c> and the count, marked <c>stale=true</c> from the second request, and <c>d\n</c>. The connection is kept.
/// </summary>
public sealed class DigestStaleRoute : IDifferentialRoute
{
    /// <inheritdoc/>
    public DifferentialRouteRank Rank => DifferentialRouteRank.Specific;

    /// <inheritdoc/>
    public bool Matches(DifferentialHttpRequest request) => request.Path == "/digeststale";

    /// <inheritdoc/>
    public async Task<bool> RespondAsync(DifferentialRouteContext context, CancellationToken cancellationToken)
    {
        var count = context.State.IncrementCounter("ds");
        var answersDigest = context.Request.Headers.GetValueOrDefault("authorization", string.Empty)
            .StartsWith("Digest ", StringComparison.Ordinal);
        var stale = count >= 2 ? ", stale=true" : string.Empty;
        var response = count >= 3 && answersDigest
            ? new DifferentialHttpResponse(200, [], "stale ok\n"u8.ToArray())
            : new DifferentialHttpResponse(
                401,
                [("WWW-Authenticate", string.Create(CultureInfo.InvariantCulture, $"Digest realm=\"r\", nonce=\"n{count}\", qop=\"auth\"{stale}"))],
                "d\n"u8.ToArray());
        await DifferentialHttpResponseWriter.WriteAsync(context.Connection, response, cancellationToken);
        return false;
    }

    [ModuleInitializer]
    internal static void Register() => DifferentialRoutes.Register(new DigestStaleRoute());
}
