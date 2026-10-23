using System.Runtime.CompilerServices;
using Curl.Conformance.Differential.Http.Core;

namespace Curl.Conformance.Differential.Http.Routes.Auth;

/// <summary>
/// server.py's <c>/ntlmish</c>: 401 with an NTLM and a Basic challenge and body <c>x</c>, whatever the
/// request; the connection is kept.
/// </summary>
public sealed class NtlmishRoute : IDifferentialRoute
{
    /// <inheritdoc/>
    public DifferentialRouteRank Rank => DifferentialRouteRank.Specific;

    /// <inheritdoc/>
    public bool Matches(DifferentialHttpRequest request) => request.Path == "/ntlmish";

    /// <inheritdoc/>
    public async Task<bool> RespondAsync(DifferentialRouteContext context, CancellationToken cancellationToken)
    {
        await DifferentialHttpResponseWriter.WriteAsync(
            context.Connection,
            new DifferentialHttpResponse(401, [("WWW-Authenticate", "NTLM"), ("WWW-Authenticate", "Basic realm=\"r\"")], "x"u8.ToArray()),
            cancellationToken);
        return false;
    }

    [ModuleInitializer]
    internal static void Register() => DifferentialRoutes.Register(new NtlmishRoute());
}
