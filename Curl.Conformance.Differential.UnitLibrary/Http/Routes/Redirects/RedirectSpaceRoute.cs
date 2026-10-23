using System.Runtime.CompilerServices;
using Curl.Conformance.Differential.Http.Core;

namespace Curl.Conformance.Differential.Http.Routes.Redirects;

/// <summary>server.py's <c>/rspace</c>: redirects to a target with a raw space in its query.</summary>
public sealed class RedirectSpaceRoute() : RedirectRoute(302)
{
    /// <inheritdoc/>
    public override bool Matches(DifferentialHttpRequest request) => request.Path == "/rspace";

    /// <inheritdoc/>
    protected override IEnumerable<(string Name, string Value)> HeadersBeforeContentLength(DifferentialResponderOptions options) =>
        [("Location", "/echo?a=b c")];

    [ModuleInitializer]
    internal static void Register() => DifferentialRoutes.Register(new RedirectSpaceRoute());
}