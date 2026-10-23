using System.Runtime.CompilerServices;
using Curl.Conformance.Differential.Http.Core;

namespace Curl.Conformance.Differential.Http.Routes.Bodies;

/// <summary>server.py's <c>/ctype</c>: <c>{"a":1}</c> as <c>application/json</c>; the connection is kept.</summary>
public sealed class ContentTypeRoute : FixedPathRoute
{
    /// <inheritdoc/>
    public override string Path => "/ctype";

    /// <inheritdoc/>
    protected override DifferentialHttpResponse BuildResponse(DifferentialHttpRequest request) =>
        new(200, [("Content-Type", "application/json")], """{"a":1}"""u8.ToArray());

    [ModuleInitializer]
    internal static void Register() => DifferentialRoutes.Register(new ContentTypeRoute());
}
