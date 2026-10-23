using System.Runtime.CompilerServices;
using Curl.Conformance.Differential.Http.Core;

namespace Curl.Conformance.Differential.Http.Routes.Redirects;

/// <summary>server.py's <c>/rrel</c>: redirects to the relative <c>../echo?x=1</c>.</summary>
public sealed class RedirectRelativeRoute() : RedirectRoute(302)
{
    /// <inheritdoc/>
    public override bool Matches(DifferentialHttpRequest request) => request.Path == "/rrel";

    /// <inheritdoc/>
    protected override IEnumerable<(string Name, string Value)> HeadersBeforeContentLength(DifferentialResponderOptions options) =>
        [("Location", "../echo?x=1")];

    [ModuleInitializer]
    internal static void Register() => DifferentialRoutes.Register(new RedirectRelativeRoute());
}