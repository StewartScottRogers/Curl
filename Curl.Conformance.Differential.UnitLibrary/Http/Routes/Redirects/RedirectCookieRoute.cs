using System.Runtime.CompilerServices;
using Curl.Conformance.Differential.Http.Core;

namespace Curl.Conformance.Differential.Http.Routes.Redirects;

/// <summary>server.py's <c>/redir-cookie</c>: redirects to <c>/echo</c> and sets the cookie <c>rc=1</c>.</summary>
public sealed class RedirectCookieRoute() : RedirectRoute(302)
{
    /// <inheritdoc/>
    public override bool Matches(DifferentialHttpRequest request) => request.Path == "/redir-cookie";

    /// <inheritdoc/>
    protected override IEnumerable<(string Name, string Value)> HeadersBeforeContentLength(DifferentialResponderOptions options) =>
        [("Location", "/echo"), ("Set-Cookie", "rc=1; Path=/")];

    [ModuleInitializer]
    internal static void Register() => DifferentialRoutes.Register(new RedirectCookieRoute());
}