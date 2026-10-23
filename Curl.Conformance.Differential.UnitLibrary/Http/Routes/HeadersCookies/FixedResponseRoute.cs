using Curl.Conformance.Differential.Http.Core;

namespace Curl.Conformance.Differential.Http.Routes.HeadersCookies;

/// <summary>
/// A server.py route that answers every request it matches with one fixed <c>resp</c> answer and keeps
/// the connection (no <c>return True</c>); each route says which paths it matches.
/// </summary>
public abstract class FixedResponseRoute(DifferentialHttpResponse response) : IDifferentialRoute
{
    /// <inheritdoc/>
    public DifferentialRouteRank Rank => DifferentialRouteRank.Specific;

    /// <inheritdoc/>
    public abstract bool Matches(DifferentialHttpRequest request);

    /// <inheritdoc/>
    public async Task<bool> RespondAsync(DifferentialRouteContext context, CancellationToken cancellationToken)
    {
        await DifferentialHttpResponseWriter.WriteAsync(context.Connection, response, cancellationToken);
        return false;
    }
}
