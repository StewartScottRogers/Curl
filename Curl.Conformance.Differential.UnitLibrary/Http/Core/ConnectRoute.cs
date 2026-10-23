using System.Runtime.CompilerServices;

namespace Curl.Conformance.Differential.Http.Core;

/// <summary>
/// server.py's <c>CONNECT</c> answer for any target no specific route took: 200 with
/// <c>Content-Length: 0</c>, then the connection closes.
/// </summary>
public sealed class ConnectRoute : IDifferentialRoute
{
    /// <inheritdoc/>
    public DifferentialRouteRank Rank => DifferentialRouteRank.Connect;

    /// <inheritdoc/>
    public bool Matches(DifferentialHttpRequest request) => request.Method == "CONNECT";

    /// <inheritdoc/>
    public async Task<bool> RespondAsync(DifferentialRouteContext context, CancellationToken cancellationToken)
    {
        await DifferentialHttpResponseWriter.WriteAsync(
            context.Connection, new DifferentialHttpResponse(200, [("Content-Length", "0")], []), cancellationToken);
        return true;
    }

    [ModuleInitializer]
    internal static void Register() => DifferentialRoutes.Register(new ConnectRoute());
}
