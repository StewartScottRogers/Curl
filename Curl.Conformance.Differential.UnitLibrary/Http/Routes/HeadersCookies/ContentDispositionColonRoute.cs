using System.Runtime.CompilerServices;
using Curl.Conformance.Differential.Http.Core;

namespace Curl.Conformance.Differential.Http.Routes.HeadersCookies;

/// <summary>server.py's <c>/cd-colon</c>: an attachment named <c>a:b?c*.txt</c>, and <c>colon\n</c>; the connection is kept.</summary>
public sealed class ContentDispositionColonRoute() : FixedResponseRoute(new DifferentialHttpResponse(
    200,
    [("Content-Disposition", "attachment; filename=\"a:b?c*.txt\"")],
    "colon\n"u8.ToArray()))
{
    /// <inheritdoc/>
    public override bool Matches(DifferentialHttpRequest request) => request.Path == "/cd-colon";

    [ModuleInitializer]
    internal static void Register() => DifferentialRoutes.Register(new ContentDispositionColonRoute());
}
