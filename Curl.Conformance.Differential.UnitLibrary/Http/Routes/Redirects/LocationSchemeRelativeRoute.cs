using System.Globalization;
using System.Runtime.CompilerServices;
using Curl.Conformance.Differential.Http.Core;

namespace Curl.Conformance.Differential.Http.Routes.Redirects;

/// <summary>server.py's <c>/loc-scheme-rel</c>: redirects to a scheme-relative URL on 127.0.0.1 and the responder's port.</summary>
public sealed class LocationSchemeRelativeRoute() : RedirectRoute(302)
{
    /// <inheritdoc/>
    public override bool Matches(DifferentialHttpRequest request) => request.Path == "/loc-scheme-rel";

    /// <inheritdoc/>
    protected override IEnumerable<(string Name, string Value)> HeadersBeforeContentLength(DifferentialResponderOptions options) =>
        [("Location", string.Create(CultureInfo.InvariantCulture, $"//127.0.0.1:{options.Port}/echo"))];

    [ModuleInitializer]
    internal static void Register() => DifferentialRoutes.Register(new LocationSchemeRelativeRoute());
}