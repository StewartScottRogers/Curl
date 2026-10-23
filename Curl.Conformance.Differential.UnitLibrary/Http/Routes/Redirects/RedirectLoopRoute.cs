using System.Runtime.CompilerServices;
using Curl.Conformance.Differential.Http.Core;

namespace Curl.Conformance.Differential.Http.Routes.Redirects;

/// <summary>server.py's <c>/rloop</c>: redirects to itself.</summary>
public sealed class RedirectLoopRoute() : RedirectRoute(302)
{
    /// <inheritdoc/>
    public override bool Matches(DifferentialHttpRequest request) => request.Path == "/rloop";

    /// <inheritdoc/>
    protected override IEnumerable<(string Name, string Value)> HeadersBeforeContentLength(DifferentialResponderOptions options) =>
        [("Location", "/rloop")];

    [ModuleInitializer]
    internal static void Register() => DifferentialRoutes.Register(new RedirectLoopRoute());
}