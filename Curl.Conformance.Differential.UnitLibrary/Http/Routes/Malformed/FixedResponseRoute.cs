using Curl.Conformance.Differential.Http.Core;

namespace Curl.Conformance.Differential.Http.Routes.Malformed;

/// <summary>
/// A server.py route that answers one path with the same bytes every time, written as they are, and then
/// keeps or closes the connection.
/// </summary>
/// <param name="path">The path the route answers.</param>
/// <param name="response">The bytes written, status line and all.</param>
/// <param name="closesConnection">Whether the connection closes afterwards (server.py's <c>return True</c>).</param>
public abstract class FixedResponseRoute(string path, byte[] response, bool closesConnection) : IDifferentialRoute
{
    /// <inheritdoc/>
    public DifferentialRouteRank Rank => DifferentialRouteRank.Specific;

    /// <inheritdoc/>
    public bool Matches(DifferentialHttpRequest request) => request.Path == path;

    /// <inheritdoc/>
    public async Task<bool> RespondAsync(DifferentialRouteContext context, CancellationToken cancellationToken)
    {
        await DifferentialHttpResponseWriter.WriteRawAsync(context.Connection, response, cancellationToken);
        return closesConnection;
    }
}
