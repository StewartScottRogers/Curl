using System.Runtime.CompilerServices;
using Curl.Conformance.Differential.Http.Core;

namespace Curl.Conformance.Differential.Http.Routes.HeadersCookies;

/// <summary>server.py's <c>/cd-reserved</c>: an attachment named <c>CON</c>, a reserved Windows device name, and <c>con\n</c>; the connection is kept.</summary>
public sealed class ContentDispositionReservedNameRoute() : FixedResponseRoute(new DifferentialHttpResponse(
    200,
    [("Content-Disposition", "attachment; filename=\"CON\"")],
    "con\n"u8.ToArray()))
{
    /// <inheritdoc/>
    public override bool Matches(DifferentialHttpRequest request) => request.Path == "/cd-reserved";

    [ModuleInitializer]
    internal static void Register() => DifferentialRoutes.Register(new ContentDispositionReservedNameRoute());
}
