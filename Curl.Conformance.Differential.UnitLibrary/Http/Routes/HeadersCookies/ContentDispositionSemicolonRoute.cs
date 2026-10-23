using System.Runtime.CompilerServices;
using Curl.Conformance.Differential.Http.Core;

namespace Curl.Conformance.Differential.Http.Routes.HeadersCookies;

/// <summary>server.py's <c>/cd-semicolon</c>: an attachment named <c>a;b.txt</c>, and <c>semi\n</c>; the connection is kept.</summary>
public sealed class ContentDispositionSemicolonRoute() : FixedResponseRoute(new DifferentialHttpResponse(
    200,
    [("Content-Disposition", "attachment; filename=\"a;b.txt\"")],
    "semi\n"u8.ToArray()))
{
    /// <inheritdoc/>
    public override bool Matches(DifferentialHttpRequest request) => request.Path == "/cd-semicolon";

    [ModuleInitializer]
    internal static void Register() => DifferentialRoutes.Register(new ContentDispositionSemicolonRoute());
}
