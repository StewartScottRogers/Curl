using System.Runtime.CompilerServices;
using Curl.Conformance.Differential.Http.Core;

namespace Curl.Conformance.Differential.Http.Routes.Auth;

/// <summary>
/// server.py's <c>/expect417</c>: 417 with <c>no expect\n</c> to a request carrying an <c>Expect</c> header,
/// otherwise <c>retried without expect\n</c>. The connection is kept.
/// </summary>
public sealed class Expect417Route : IDifferentialRoute
{
    /// <inheritdoc/>
    public DifferentialRouteRank Rank => DifferentialRouteRank.Specific;

    /// <inheritdoc/>
    public bool Matches(DifferentialHttpRequest request) => request.Path == "/expect417";

    /// <inheritdoc/>
    public async Task<bool> RespondAsync(DifferentialRouteContext context, CancellationToken cancellationToken)
    {
        var response = context.Request.Headers.ContainsKey("expect")
            ? new DifferentialHttpResponse(417, [], "no expect\n"u8.ToArray())
            : new DifferentialHttpResponse(200, [], "retried without expect\n"u8.ToArray());
        await DifferentialHttpResponseWriter.WriteAsync(context.Connection, response, cancellationToken);
        return false;
    }

    [ModuleInitializer]
    internal static void Register() => DifferentialRoutes.Register(new Expect417Route());
}
