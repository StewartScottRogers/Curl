using System.Runtime.CompilerServices;
using Curl.Conformance.Differential.Http.Core;

namespace Curl.Conformance.Differential.Http.Routes.Redirects;

/// <summary>server.py's <c>/loc-utf8</c>: redirects to <c>/echo?q=</c> and the UTF-8 bytes of an e-acute read as Latin-1 characters, which the UTF-8 encoded head sends again as C3 83 C2 A9, as server.py's <c>encode("utf8").decode("latin1")</c> does.</summary>
public sealed class LocationUtf8Route() : RedirectRoute(302)
{
    /// <inheritdoc/>
    public override bool Matches(DifferentialHttpRequest request) => request.Path == "/loc-utf8";

    /// <inheritdoc/>
    protected override IEnumerable<(string Name, string Value)> HeadersBeforeContentLength(DifferentialResponderOptions options) =>
        [("Location", "/echo?q=\u00C3\u00A9")];

    [ModuleInitializer]
    internal static void Register() => DifferentialRoutes.Register(new LocationUtf8Route());
}