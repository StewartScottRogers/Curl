using System.Runtime.CompilerServices;
using Curl.Conformance.Differential.Http.Core;

namespace Curl.Conformance.Differential.Http.Routes.Bodies;

/// <summary>server.py's <c>/empty</c>: an empty body with its own <c>Content-Length: 0</c>; the connection is kept.</summary>
public sealed class EmptyRoute : FixedPathRoute
{
    /// <inheritdoc/>
    public override string Path => "/empty";

    /// <inheritdoc/>
    protected override DifferentialHttpResponse BuildResponse(DifferentialHttpRequest request) =>
        new(200, [("Content-Length", "0")], []);

    [ModuleInitializer]
    internal static void Register() => DifferentialRoutes.Register(new EmptyRoute());
}
