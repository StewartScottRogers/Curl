using System.Runtime.CompilerServices;
using Curl.Conformance.Differential.Http.Core;

namespace Curl.Conformance.Differential.Http.Routes.Redirects;

/// <summary>server.py's <c>/loc-query</c>: redirects to the query-only reference <c>?x=2</c>.</summary>
public sealed class LocationQueryRoute() : RedirectRoute(302)
{
    /// <inheritdoc/>
    public override bool Matches(DifferentialHttpRequest request) => request.Path == "/loc-query";

    /// <inheritdoc/>
    protected override IEnumerable<(string Name, string Value)> HeadersBeforeContentLength(DifferentialResponderOptions options) =>
        [("Location", "?x=2")];

    [ModuleInitializer]
    internal static void Register() => DifferentialRoutes.Register(new LocationQueryRoute());
}