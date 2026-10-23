using System.Runtime.CompilerServices;
using Curl.Conformance.Differential.Http.Core;

namespace Curl.Conformance.Differential.Http.Routes.HeadersCookies;

/// <summary>server.py's <c>/cdbad</c>: an attachment named <c>../../evil.txt</c>, and <c>evil\n</c>; the connection is kept.</summary>
public sealed class ContentDispositionParentPathRoute() : FixedResponseRoute(new DifferentialHttpResponse(
    200,
    [("Content-Disposition", "attachment; filename=\"../../evil.txt\"")],
    "evil\n"u8.ToArray()))
{
    /// <inheritdoc/>
    public override bool Matches(DifferentialHttpRequest request) => request.Path == "/cdbad";

    [ModuleInitializer]
    internal static void Register() => DifferentialRoutes.Register(new ContentDispositionParentPathRoute());
}
