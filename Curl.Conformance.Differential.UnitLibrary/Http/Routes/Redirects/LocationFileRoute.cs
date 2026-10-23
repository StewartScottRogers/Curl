using System.Runtime.CompilerServices;
using Curl.Conformance.Differential.Http.Core;

namespace Curl.Conformance.Differential.Http.Routes.Redirects;

/// <summary>server.py's <c>/loc-file</c>: redirects to <c>file:///etc/passwd</c>.</summary>
public sealed class LocationFileRoute() : RedirectRoute(302)
{
    /// <inheritdoc/>
    public override bool Matches(DifferentialHttpRequest request) => request.Path == "/loc-file";

    /// <inheritdoc/>
    protected override IEnumerable<(string Name, string Value)> HeadersBeforeContentLength(DifferentialResponderOptions options) =>
        [("Location", "file:///etc/passwd")];

    [ModuleInitializer]
    internal static void Register() => DifferentialRoutes.Register(new LocationFileRoute());
}