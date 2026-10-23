using System.Runtime.CompilerServices;
using Curl.Conformance.Differential.Http.Core;

namespace Curl.Conformance.Differential.Http.Routes.Redirects;

/// <summary>server.py's <c>/auth-redir</c>: redirects to <c>/needauth</c>.</summary>
public sealed class AuthRedirectRoute() : RedirectRoute(302)
{
    /// <inheritdoc/>
    public override bool Matches(DifferentialHttpRequest request) => request.Path == "/auth-redir";

    /// <inheritdoc/>
    protected override IEnumerable<(string Name, string Value)> HeadersBeforeContentLength(DifferentialResponderOptions options) =>
        [("Location", "/needauth")];

    [ModuleInitializer]
    internal static void Register() => DifferentialRoutes.Register(new AuthRedirectRoute());
}