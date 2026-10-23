using System.Globalization;
using System.Runtime.CompilerServices;
using Curl.Conformance.Differential.Http.Core;

namespace Curl.Conformance.Differential.Http.Routes.Redirects;

/// <summary>server.py's <c>/rother</c>: redirects to <c>localhost</c> on the responder's port, another host name for the same server.</summary>
public sealed class RedirectOtherHostRoute() : RedirectRoute(302)
{
    /// <inheritdoc/>
    public override bool Matches(DifferentialHttpRequest request) => request.Path == "/rother";

    /// <inheritdoc/>
    protected override IEnumerable<(string Name, string Value)> HeadersBeforeContentLength(DifferentialResponderOptions options) =>
        [("Location", string.Create(CultureInfo.InvariantCulture, $"http://localhost:{options.Port}/echo"))];

    [ModuleInitializer]
    internal static void Register() => DifferentialRoutes.Register(new RedirectOtherHostRoute());
}