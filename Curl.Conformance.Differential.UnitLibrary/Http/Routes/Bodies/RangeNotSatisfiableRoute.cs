using System.Runtime.CompilerServices;
using Curl.Conformance.Differential.Http.Core;

namespace Curl.Conformance.Differential.Http.Routes.Bodies;

/// <summary>server.py's <c>/range416</c>: status 416 (reason <c>Status</c>, as server.py's table has none) with <c>Content-Range: bytes */10</c> and an empty body; the connection is kept.</summary>
public sealed class RangeNotSatisfiableRoute : FixedPathRoute
{
    /// <inheritdoc/>
    public override string Path => "/range416";

    /// <inheritdoc/>
    protected override DifferentialHttpResponse BuildResponse(DifferentialHttpRequest request) =>
        new(416, [("Content-Range", "bytes */10")], []);

    [ModuleInitializer]
    internal static void Register() => DifferentialRoutes.Register(new RangeNotSatisfiableRoute());
}
