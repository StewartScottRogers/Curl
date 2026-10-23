using System.Runtime.CompilerServices;
using Curl.Conformance.Differential.Http.Core;

namespace Curl.Conformance.Differential.Http.Routes.Bodies;

/// <summary>server.py's <c>/binary</c>: every byte value 0 to 255 once, in order; the connection is kept.</summary>
public sealed class BinaryRoute : FixedPathRoute
{
    /// <inheritdoc/>
    public override string Path => "/binary";

    /// <inheritdoc/>
    protected override DifferentialHttpResponse BuildResponse(DifferentialHttpRequest request) =>
        new(200, [], [.. Enumerable.Range(0, 256).Select(value => (byte)value)]);

    [ModuleInitializer]
    internal static void Register() => DifferentialRoutes.Register(new BinaryRoute());
}
