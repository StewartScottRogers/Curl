using System.Runtime.CompilerServices;
using Curl.Conformance.Differential.Http.Core;

namespace Curl.Conformance.Differential.Http.Routes.HeadersCookies;

/// <summary>server.py's <c>/cd-long</c>: an attachment named 300 <c>n</c> then <c>.txt</c>, and <c>long\n</c>; the connection is kept.</summary>
public sealed class ContentDispositionLongFilenameRoute() : FixedResponseRoute(new DifferentialHttpResponse(
    200,
    [("Content-Disposition", "attachment; filename=\"" + new string('n', 300) + ".txt\"")],
    "long\n"u8.ToArray()))
{
    /// <inheritdoc/>
    public override bool Matches(DifferentialHttpRequest request) => request.Path == "/cd-long";

    [ModuleInitializer]
    internal static void Register() => DifferentialRoutes.Register(new ContentDispositionLongFilenameRoute());
}
