using System.Runtime.CompilerServices;
using Curl.Conformance.Differential.Http.Core;

namespace Curl.Conformance.Differential.Http.Routes.Bodies;

/// <summary>server.py's <c>/lm</c>: a body with a fixed <c>Last-Modified</c> and <c>ETag: "v1"</c>; the connection is kept.</summary>
public sealed class LastModifiedRoute : FixedPathRoute
{
    /// <inheritdoc/>
    public override string Path => "/lm";

    /// <inheritdoc/>
    protected override DifferentialHttpResponse BuildResponse(DifferentialHttpRequest request) =>
        new(200, [("Last-Modified", "Wed, 21 Oct 2015 07:28:00 GMT"), ("ETag", "\"v1\"")], "lm body\n"u8.ToArray());

    [ModuleInitializer]
    internal static void Register() => DifferentialRoutes.Register(new LastModifiedRoute());
}
