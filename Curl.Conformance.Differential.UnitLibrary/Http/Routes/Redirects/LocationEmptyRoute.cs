using System.Runtime.CompilerServices;
using Curl.Conformance.Differential.Http.Core;

namespace Curl.Conformance.Differential.Http.Routes.Redirects;

/// <summary>server.py's <c>/loc-empty</c>: answers 302 with an empty <c>Location</c>.</summary>
public sealed class LocationEmptyRoute() : RedirectRoute(302)
{
    /// <inheritdoc/>
    public override bool Matches(DifferentialHttpRequest request) => request.Path == "/loc-empty";

    /// <inheritdoc/>
    protected override IEnumerable<(string Name, string Value)> HeadersBeforeContentLength(DifferentialResponderOptions options) =>
        [("Location", string.Empty)];

    [ModuleInitializer]
    internal static void Register() => DifferentialRoutes.Register(new LocationEmptyRoute());
}