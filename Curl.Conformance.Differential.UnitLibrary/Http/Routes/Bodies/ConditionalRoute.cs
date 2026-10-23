using System.Runtime.CompilerServices;
using Curl.Conformance.Differential.Http.Core;

namespace Curl.Conformance.Differential.Http.Routes.Bodies;

/// <summary>
/// server.py's <c>/cond</c>: 304 with <c>ETag: "v1"</c> when <c>If-None-Match</c> is exactly <c>"v1"</c>,
/// otherwise <c>fresh\n</c> with the same <c>ETag</c>; the connection is kept.
/// </summary>
public sealed class ConditionalRoute : FixedPathRoute
{
    private const string EntityTag = "\"v1\"";

    /// <inheritdoc/>
    public override string Path => "/cond";

    /// <inheritdoc/>
    protected override DifferentialHttpResponse BuildResponse(DifferentialHttpRequest request) =>
        request.Headers.GetValueOrDefault("if-none-match") == EntityTag
            ? new(304, [("ETag", EntityTag)], [])
            : new(200, [("ETag", EntityTag)], "fresh\n"u8.ToArray());

    [ModuleInitializer]
    internal static void Register() => DifferentialRoutes.Register(new ConditionalRoute());
}
