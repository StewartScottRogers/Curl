using System.Runtime.CompilerServices;
using Curl.Conformance.Differential.Http.Core;

namespace Curl.Conformance.Differential.Http.Routes.HeadersCookies;

/// <summary>server.py's <c>/cd</c>: an attachment named <c>named file.txt</c> with a <c>Last-Modified</c> date, and <c>cd body\n</c>; the connection is kept.</summary>
public sealed class ContentDispositionRoute() : FixedResponseRoute(new DifferentialHttpResponse(
    200,
    [("Content-Disposition", "attachment; filename=\"named file.txt\""), ("Last-Modified", "Wed, 21 Oct 2015 07:28:00 GMT")],
    "cd body\n"u8.ToArray()))
{
    /// <inheritdoc/>
    public override bool Matches(DifferentialHttpRequest request) => request.Path == "/cd";

    [ModuleInitializer]
    internal static void Register() => DifferentialRoutes.Register(new ContentDispositionRoute());
}
