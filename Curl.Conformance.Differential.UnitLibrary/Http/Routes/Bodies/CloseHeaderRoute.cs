using System.Runtime.CompilerServices;
using Curl.Conformance.Differential.Http.Core;

namespace Curl.Conformance.Differential.Http.Routes.Bodies;

/// <summary>server.py's <c>/close-hdr</c>: <c>closing\n</c> with the route's own <c>Connection: close</c>, then the connection closes.</summary>
public sealed class CloseHeaderRoute : FixedPathRoute
{
    /// <inheritdoc/>
    public override string Path => "/close-hdr";

    /// <inheritdoc/>
    protected override bool ClosesConnection => true;

    /// <inheritdoc/>
    protected override DifferentialHttpResponse BuildResponse(DifferentialHttpRequest request) =>
        new(200, [("Connection", "close")], "closing\n"u8.ToArray());

    [ModuleInitializer]
    internal static void Register() => DifferentialRoutes.Register(new CloseHeaderRoute());
}
