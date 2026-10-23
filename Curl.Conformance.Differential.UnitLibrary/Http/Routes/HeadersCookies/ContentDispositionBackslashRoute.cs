using System.Runtime.CompilerServices;
using Curl.Conformance.Differential.Http.Core;

namespace Curl.Conformance.Differential.Http.Routes.HeadersCookies;

/// <summary>server.py's <c>/cd-backslash</c>: an attachment named <c>..\\evil.txt</c> (two backslashes), and <c>bs\n</c>; the connection is kept.</summary>
public sealed class ContentDispositionBackslashRoute() : FixedResponseRoute(new DifferentialHttpResponse(
    200,
    [("Content-Disposition", "attachment; filename=\"..\\\\evil.txt\"")],
    "bs\n"u8.ToArray()))
{
    /// <inheritdoc/>
    public override bool Matches(DifferentialHttpRequest request) => request.Path == "/cd-backslash";

    [ModuleInitializer]
    internal static void Register() => DifferentialRoutes.Register(new ContentDispositionBackslashRoute());
}
