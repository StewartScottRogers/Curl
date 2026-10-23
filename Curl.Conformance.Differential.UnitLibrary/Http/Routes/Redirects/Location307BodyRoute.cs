using System.Runtime.CompilerServices;
using Curl.Conformance.Differential.Http.Core;

namespace Curl.Conformance.Differential.Http.Routes.Redirects;

/// <summary>server.py's <c>/loc-307-body</c>: answers 307 to <c>/echo</c>.</summary>
public sealed class Location307BodyRoute() : RedirectRoute(307)
{
    /// <inheritdoc/>
    public override bool Matches(DifferentialHttpRequest request) => request.Path == "/loc-307-body";

    /// <inheritdoc/>
    protected override IEnumerable<(string Name, string Value)> HeadersBeforeContentLength(DifferentialResponderOptions options) =>
        [("Location", "/echo")];

    [ModuleInitializer]
    internal static void Register() => DifferentialRoutes.Register(new Location307BodyRoute());
}