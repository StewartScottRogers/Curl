using System.Runtime.CompilerServices;
using Curl.Conformance.Differential.Http.Core;

namespace Curl.Conformance.Differential.Http.Routes.Bodies;

/// <summary>server.py's <c>/jsonish</c>: a small JSON body as <c>application/json; charset=utf-8</c>; the connection is kept.</summary>
public sealed class JsonishRoute : FixedPathRoute
{
    /// <inheritdoc/>
    public override string Path => "/jsonish";

    /// <inheritdoc/>
    protected override DifferentialHttpResponse BuildResponse(DifferentialHttpRequest request) =>
        new(200, [("Content-Type", "application/json; charset=utf-8")], """{"k":[1,2,{"z":null}]}"""u8.ToArray());

    [ModuleInitializer]
    internal static void Register() => DifferentialRoutes.Register(new JsonishRoute());
}
