using System.Runtime.CompilerServices;

namespace Curl.Conformance.Differential.Http.Core;

/// <summary>
/// server.py's <c>/echo</c> and <c>/</c>: <c>ok\n</c> as text, or for <c>HEAD</c> the same head with
/// <c>Content-Length: 3</c> and no body; the connection is kept.
/// </summary>
public sealed class EchoRoute : IDifferentialRoute
{
    /// <inheritdoc/>
    public DifferentialRouteRank Rank => DifferentialRouteRank.Specific;

    /// <inheritdoc/>
    public bool Matches(DifferentialHttpRequest request) => request.Path is "/echo" or "/";

    /// <inheritdoc/>
    public async Task<bool> RespondAsync(DifferentialRouteContext context, CancellationToken cancellationToken)
    {
        var response = context.Request.Method == "HEAD"
            ? new DifferentialHttpResponse(200, [("Content-Type", "text/plain"), ("Content-Length", "3")], [])
            : new DifferentialHttpResponse(200, [("Content-Type", "text/plain")], "ok\n"u8.ToArray());
        await DifferentialHttpResponseWriter.WriteAsync(context.Connection, response, cancellationToken);
        return false;
    }

    [ModuleInitializer]
    internal static void Register() => DifferentialRoutes.Register(new EchoRoute());
}
