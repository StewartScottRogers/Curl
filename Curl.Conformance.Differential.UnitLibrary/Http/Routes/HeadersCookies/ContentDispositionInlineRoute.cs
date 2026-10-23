using System.Runtime.CompilerServices;
using Curl.Conformance.Differential.Http.Core;

namespace Curl.Conformance.Differential.Http.Routes.HeadersCookies;

/// <summary>server.py's <c>/cd-nofile</c>: <c>Content-Disposition: inline</c> with no file name, and <c>cd\n</c>; the connection is kept.</summary>
public sealed class ContentDispositionInlineRoute() : FixedResponseRoute(new DifferentialHttpResponse(
    200,
    [("Content-Disposition", "inline")],
    "cd\n"u8.ToArray()))
{
    /// <inheritdoc/>
    public override bool Matches(DifferentialHttpRequest request) => request.Path == "/cd-nofile";

    [ModuleInitializer]
    internal static void Register() => DifferentialRoutes.Register(new ContentDispositionInlineRoute());
}
