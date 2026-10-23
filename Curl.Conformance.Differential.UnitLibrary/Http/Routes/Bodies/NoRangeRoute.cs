using System.Runtime.CompilerServices;
using Curl.Conformance.Differential.Http.Core;

namespace Curl.Conformance.Differential.Http.Routes.Bodies;

/// <summary>server.py's <c>/norange</c>: the full body whatever <c>Range</c> asks; the connection is kept.</summary>
public sealed class NoRangeRoute : FixedPathRoute
{
    /// <inheritdoc/>
    public override string Path => "/norange";

    /// <inheritdoc/>
    protected override DifferentialHttpResponse BuildResponse(DifferentialHttpRequest request) =>
        new(200, [], "full body ignoring range\n"u8.ToArray());

    [ModuleInitializer]
    internal static void Register() => DifferentialRoutes.Register(new NoRangeRoute());
}
