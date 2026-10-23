using System.Globalization;
using System.Runtime.CompilerServices;
using Curl.Conformance.Differential.Http.Core;

namespace Curl.Conformance.Differential.Http.Routes.Redirects;

/// <summary>server.py's <c>/rabs</c>: redirects to an absolute URL on 127.0.0.1 and the responder's port, with a fragment.</summary>
public sealed class RedirectAbsoluteRoute() : RedirectRoute(302)
{
    /// <inheritdoc/>
    public override bool Matches(DifferentialHttpRequest request) => request.Path == "/rabs";

    /// <inheritdoc/>
    protected override IEnumerable<(string Name, string Value)> HeadersBeforeContentLength(DifferentialResponderOptions options) =>
        [("Location", string.Create(CultureInfo.InvariantCulture, $"http://127.0.0.1:{options.Port}/echo#frag"))];

    [ModuleInitializer]
    internal static void Register() => DifferentialRoutes.Register(new RedirectAbsoluteRoute());
}