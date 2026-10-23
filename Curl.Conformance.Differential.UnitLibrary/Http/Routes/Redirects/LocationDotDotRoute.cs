using System.Runtime.CompilerServices;
using Curl.Conformance.Differential.Http.Core;

namespace Curl.Conformance.Differential.Http.Routes.Redirects;

/// <summary>server.py's <c>/loc-dotdot</c>: redirects to <c>../../x/./y</c>, a reference with dot segments.</summary>
public sealed class LocationDotDotRoute() : RedirectRoute(302)
{
    /// <inheritdoc/>
    public override bool Matches(DifferentialHttpRequest request) => request.Path == "/loc-dotdot";

    /// <inheritdoc/>
    protected override IEnumerable<(string Name, string Value)> HeadersBeforeContentLength(DifferentialResponderOptions options) =>
        [("Location", "../../x/./y")];

    [ModuleInitializer]
    internal static void Register() => DifferentialRoutes.Register(new LocationDotDotRoute());
}