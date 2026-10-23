using System.Globalization;
using System.Runtime.CompilerServices;
using Curl.Conformance.Differential.Http.Core;

namespace Curl.Conformance.Differential.Http.Routes.Redirects;

/// <summary>server.py's <c>/loc-userinfo</c>: redirects to a URL with user info <c>u:p</c> on 127.0.0.1 and the responder's port.</summary>
public sealed class LocationUserInfoRoute() : RedirectRoute(302)
{
    /// <inheritdoc/>
    public override bool Matches(DifferentialHttpRequest request) => request.Path == "/loc-userinfo";

    /// <inheritdoc/>
    protected override IEnumerable<(string Name, string Value)> HeadersBeforeContentLength(DifferentialResponderOptions options) =>
        [("Location", string.Create(CultureInfo.InvariantCulture, $"http://u:p@127.0.0.1:{options.Port}/echo"))];

    [ModuleInitializer]
    internal static void Register() => DifferentialRoutes.Register(new LocationUserInfoRoute());
}