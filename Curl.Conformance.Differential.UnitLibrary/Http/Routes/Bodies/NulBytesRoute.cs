using System.Runtime.CompilerServices;
using Curl.Conformance.Differential.Http.Core;

namespace Curl.Conformance.Differential.Http.Routes.Bodies;

/// <summary>server.py's <c>/nul</c>: a body with NUL bytes between its letters; the connection is kept.</summary>
public sealed class NulBytesRoute : FixedPathRoute
{
    /// <inheritdoc/>
    public override string Path => "/nul";

    /// <inheritdoc/>
    protected override DifferentialHttpResponse BuildResponse(DifferentialHttpRequest request) =>
        new(200, [], "a\0b\0c\n"u8.ToArray());

    [ModuleInitializer]
    internal static void Register() => DifferentialRoutes.Register(new NulBytesRoute());
}
