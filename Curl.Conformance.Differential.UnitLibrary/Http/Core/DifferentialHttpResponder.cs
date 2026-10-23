namespace Curl.Conformance.Differential.Http.Core;

/// <summary>
/// Serves one connection as server.py's handler does: reads requests in a keep-alive loop, logs each one,
/// and answers it with the first route that matches, until a route closes the connection, a request
/// cannot be parsed, no route matches, or the stream ends or fails.
/// </summary>
public sealed class DifferentialHttpResponder(
    IReadOnlyList<IDifferentialRoute> routes, DifferentialResponderState state, DifferentialResponderOptions options)
{
    /// <summary>Serves <paramref name="connection"/> until it closes.</summary>
    public async Task RespondAsync(Stream connection, CancellationToken cancellationToken)
    {
        var reader = new DifferentialHttpLineReader(connection);
        try
        {
            while (await DifferentialHttpRequestReader.ReadAsync(reader, connection, cancellationToken) is { } read)
            {
                state.LogRequest(read.LoggedBytes);
                if (read.Request is not { } request || await RouteAsync(request, connection, cancellationToken))
                {
                    return;
                }
            }
        }
        catch (IOException)
        {
            // server.py's handler returns on a broken connection.
        }
    }

    private async Task<bool> RouteAsync(DifferentialHttpRequest request, Stream connection, CancellationToken cancellationToken)
    {
        var route = routes.FirstOrDefault(candidate => candidate.Matches(request));
        return route is null
            || await route.RespondAsync(new DifferentialRouteContext(request, connection, state, options), cancellationToken);
    }
}
