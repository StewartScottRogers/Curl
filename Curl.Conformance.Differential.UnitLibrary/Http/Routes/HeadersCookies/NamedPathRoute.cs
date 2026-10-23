using System.Runtime.CompilerServices;
using Curl.Conformance.Differential.Http.Core;

namespace Curl.Conformance.Differential.Http.Routes.HeadersCookies;

/// <summary>
/// server.py's prefix route for paths starting <c>/name:</c>, <c>/q?</c>, <c>/CON</c> or <c>/trail.</c>:
/// <c>named\n</c> with no headers of its own; the connection is kept. A request's path never holds the
/// <c>?</c> its query was split at, so <c>/q?</c> never matches a request, as in server.py.
/// </summary>
public sealed class NamedPathRoute() : FixedResponseRoute(new DifferentialHttpResponse(200, [], "named\n"u8.ToArray()))
{
    /// <inheritdoc/>
    public override bool Matches(DifferentialHttpRequest request) =>
        request.Path.StartsWith("/name:", StringComparison.Ordinal)
        || request.Path.StartsWith("/q?", StringComparison.Ordinal)
        || request.Path.StartsWith("/CON", StringComparison.Ordinal)
        || request.Path.StartsWith("/trail.", StringComparison.Ordinal);

    [ModuleInitializer]
    internal static void Register() => DifferentialRoutes.Register(new NamedPathRoute());
}
