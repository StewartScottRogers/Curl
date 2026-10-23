using System.Runtime.CompilerServices;
using Curl.Conformance.Differential.Http.Core;

namespace Curl.Conformance.Differential.Http.Routes.Redirects;

/// <summary>server.py's <c>/rftp</c>: redirects to an <c>ftp://</c> URL.</summary>
public sealed class RedirectFtpRoute() : RedirectRoute(302)
{
    /// <inheritdoc/>
    public override bool Matches(DifferentialHttpRequest request) => request.Path == "/rftp";

    /// <inheritdoc/>
    protected override IEnumerable<(string Name, string Value)> HeadersBeforeContentLength(DifferentialResponderOptions options) =>
        [("Location", "ftp://127.0.0.1:1/x")];

    [ModuleInitializer]
    internal static void Register() => DifferentialRoutes.Register(new RedirectFtpRoute());
}