using Curl.Conformance.Differential.Http.Core;

namespace Curl.Conformance.Differential.Http.Routes.Bodies;

/// <summary>
/// A server.py route that matches one exact path and answers with one <c>resp</c> call: the subclass
/// names the path, builds the response from the request, and says whether the connection closes afterwards.
/// </summary>
public abstract class FixedPathRoute : IDifferentialRoute
{
    /// <inheritdoc/>
    public DifferentialRouteRank Rank => DifferentialRouteRank.Specific;

    /// <summary>Gets the path this route answers.</summary>
    public abstract string Path { get; }

    /// <summary>Gets whether the connection closes after the answer (server.py's <c>return True</c>).</summary>
    protected virtual bool ClosesConnection => false;

    /// <inheritdoc/>
    public bool Matches(DifferentialHttpRequest request) => request.Path == Path;

    /// <inheritdoc/>
    public virtual async Task<bool> RespondAsync(DifferentialRouteContext context, CancellationToken cancellationToken)
    {
        await DifferentialHttpResponseWriter.WriteAsync(context.Connection, BuildResponse(context.Request), cancellationToken);
        return ClosesConnection;
    }

    /// <summary>Builds the answer to <paramref name="request"/>.</summary>
    protected abstract DifferentialHttpResponse BuildResponse(DifferentialHttpRequest request);
}
