using System.Runtime.CompilerServices;
using Curl.Conformance.Differential.Http.Core;

namespace Curl.Conformance.Differential.Http.Routes.Redirects;

/// <summary>server.py's <c>/loc-multi</c>: answers 302 with two <c>Location</c> headers, <c>/echo</c> then <c>/default</c>.</summary>
public sealed class LocationMultipleRoute() : RedirectRoute(302)
{
    /// <inheritdoc/>
    public override bool Matches(DifferentialHttpRequest request) => request.Path == "/loc-multi";

    /// <inheritdoc/>
    protected override IEnumerable<(string Name, string Value)> HeadersBeforeContentLength(DifferentialResponderOptions options) =>
        [("Location", "/echo"), ("Location", "/default")];

    [ModuleInitializer]
    internal static void Register() => DifferentialRoutes.Register(new LocationMultipleRoute());
}