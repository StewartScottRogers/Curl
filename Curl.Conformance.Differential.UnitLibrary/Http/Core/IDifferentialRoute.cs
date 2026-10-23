namespace Curl.Conformance.Differential.Http.Core;

/// <summary>
/// One of server.py's routes: it says whether it answers a request, writes the answer, and says whether
/// the connection closes afterwards (server.py's <c>return True</c>). A route file adds its route to
/// <see cref="DifferentialRoutes"/> from a <c>[ModuleInitializer]</c>.
/// </summary>
public interface IDifferentialRoute
{
    /// <summary>Gets where the route sits in the order routes are tried.</summary>
    DifferentialRouteRank Rank { get; }

    /// <summary>Whether this route answers <paramref name="request"/>.</summary>
    bool Matches(DifferentialHttpRequest request);

    /// <summary>Writes the answer; returns <see langword="true"/> when the connection closes afterwards.</summary>
    Task<bool> RespondAsync(DifferentialRouteContext context, CancellationToken cancellationToken);
}
