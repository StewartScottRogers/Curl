using Curl.Conformance.Differential.Http.Core;

namespace Curl.Conformance.Differential.Http.Routes.Redirects;

/// <summary>
/// A server.py redirect route: answers its status with its headers then <c>Content-Length: 0</c>, an empty
/// body, and keeps the connection, as every redirect line of server.py's <c>route</c> does.
/// </summary>
/// <param name="status">The status the route answers with unless <see cref="StatusFor"/> says otherwise.</param>
public abstract class RedirectRoute(int status) : IDifferentialRoute
{
    /// <inheritdoc/>
    public DifferentialRouteRank Rank => DifferentialRouteRank.Specific;

    /// <inheritdoc/>
    public abstract bool Matches(DifferentialHttpRequest request);

    /// <inheritdoc/>
    public async Task<bool> RespondAsync(DifferentialRouteContext context, CancellationToken cancellationToken)
    {
        var response = new DifferentialHttpResponse(
            StatusFor(context.Request), [.. HeadersBeforeContentLength(context.Options), ("Content-Length", "0")], []);
        await DifferentialHttpResponseWriter.WriteAsync(context.Connection, response, cancellationToken);
        return false;
    }

    /// <summary>The status the route answers <paramref name="request"/> with.</summary>
    protected virtual int StatusFor(DifferentialHttpRequest request) => status;

    /// <summary>The headers server.py sends before its <c>Content-Length: 0</c>, in order.</summary>
    protected abstract IEnumerable<(string Name, string Value)> HeadersBeforeContentLength(DifferentialResponderOptions options);
}
