using System.Runtime.CompilerServices;

namespace Curl.Conformance.Differential.Http.Core;

/// <summary>server.py's answer when no other route matches: <c>default\n</c> as text; the connection is kept.</summary>
public sealed class DefaultRoute : IDifferentialRoute
{
    /// <inheritdoc/>
    public DifferentialRouteRank Rank => DifferentialRouteRank.Default;

    /// <inheritdoc/>
    public bool Matches(DifferentialHttpRequest request) => true;

    /// <inheritdoc/>
    public async Task<bool> RespondAsync(DifferentialRouteContext context, CancellationToken cancellationToken)
    {
        await DifferentialHttpResponseWriter.WriteAsync(
            context.Connection,
            new DifferentialHttpResponse(200, [("Content-Type", "text/plain")], "default\n"u8.ToArray()),
            cancellationToken);
        return false;
    }

    [ModuleInitializer]
    internal static void Register() => DifferentialRoutes.Register(new DefaultRoute());
}
