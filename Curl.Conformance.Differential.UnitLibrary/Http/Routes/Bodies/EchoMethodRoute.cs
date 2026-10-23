using System.Runtime.CompilerServices;
using Curl.Conformance.Differential.Http.Core;

namespace Curl.Conformance.Differential.Http.Routes.Bodies;

/// <summary>server.py's <c>/echo-method</c>: the request method in <c>X-Method</c> and an empty body; the connection is kept.</summary>
public sealed class EchoMethodRoute : FixedPathRoute
{
    /// <inheritdoc/>
    public override string Path => "/echo-method";

    /// <inheritdoc/>
    protected override DifferentialHttpResponse BuildResponse(DifferentialHttpRequest request) =>
        new(200, [("X-Method", request.Method)], []);

    [ModuleInitializer]
    internal static void Register() => DifferentialRoutes.Register(new EchoMethodRoute());
}
