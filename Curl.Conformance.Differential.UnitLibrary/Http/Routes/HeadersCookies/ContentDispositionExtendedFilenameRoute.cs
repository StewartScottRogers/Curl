using System.Runtime.CompilerServices;
using Curl.Conformance.Differential.Http.Core;

namespace Curl.Conformance.Differential.Http.Routes.HeadersCookies;

/// <summary>server.py's <c>/ct-cd-star</c>: an attachment named only by <c>filename*=UTF-8''na%C3%AFve%20file.txt</c>, and <c>star\n</c>; the connection is kept.</summary>
public sealed class ContentDispositionExtendedFilenameRoute() : FixedResponseRoute(new DifferentialHttpResponse(
    200,
    [("Content-Disposition", "attachment; filename*=UTF-8''na%C3%AFve%20file.txt")],
    "star\n"u8.ToArray()))
{
    /// <inheritdoc/>
    public override bool Matches(DifferentialHttpRequest request) => request.Path == "/ct-cd-star";

    [ModuleInitializer]
    internal static void Register() => DifferentialRoutes.Register(new ContentDispositionExtendedFilenameRoute());
}
