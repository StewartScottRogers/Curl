using System.Runtime.CompilerServices;
using Curl.Conformance.Differential.Http.Core;

namespace Curl.Conformance.Differential.Http.Routes.Bodies;

/// <summary>server.py's <c>/big</c>: 3,000,000 bytes of <c>i % 251</c> as <c>application/octet-stream</c>; the connection is kept.</summary>
public sealed class BigBodyRoute : FixedPathRoute
{
    /// <inheritdoc/>
    public override string Path => "/big";

    /// <inheritdoc/>
    protected override DifferentialHttpResponse BuildResponse(DifferentialHttpRequest request) =>
        new(200, [("Content-Type", "application/octet-stream")], [.. Enumerable.Range(0, 3_000_000).Select(index => (byte)(index % 251))]);

    [ModuleInitializer]
    internal static void Register() => DifferentialRoutes.Register(new BigBodyRoute());
}
