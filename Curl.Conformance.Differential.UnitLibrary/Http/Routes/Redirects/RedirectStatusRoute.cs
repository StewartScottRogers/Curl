using System.Globalization;
using System.Runtime.CompilerServices;
using Curl.Conformance.Differential.Http.Core;

namespace Curl.Conformance.Differential.Http.Routes.Redirects;

/// <summary>server.py's <c>/r301</c> to <c>/r308</c> (<c>^/r30[1-8]$</c>): answers that status with <c>Location: /echo</c>.</summary>
public sealed class RedirectStatusRoute : RedirectRoute
{
    /// <summary>Initializes the route; its status comes from the path, never from the base.</summary>
    public RedirectStatusRoute()
        : base(0)
    {
    }

    /// <inheritdoc/>
    public override bool Matches(DifferentialHttpRequest request) =>
        request.Path.Length == 5 && request.Path.StartsWith("/r30", StringComparison.Ordinal) && request.Path[4] is >= '1' and <= '8';

    /// <inheritdoc/>
    protected override int StatusFor(DifferentialHttpRequest request) => int.Parse(request.Path.AsSpan(2, 3), CultureInfo.InvariantCulture);

    /// <inheritdoc/>
    protected override IEnumerable<(string Name, string Value)> HeadersBeforeContentLength(DifferentialResponderOptions options) =>
        [("Location", "/echo")];

    [ModuleInitializer]
    internal static void Register() => DifferentialRoutes.Register(new RedirectStatusRoute());
}
