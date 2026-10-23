using System.Runtime.CompilerServices;
using Curl.Conformance.Differential.Http.Core;

namespace Curl.Conformance.Differential.Http.Routes.HeadersCookies;

/// <summary>server.py's <c>/refresh</c>: <c>Refresh: 0; url=/echo</c> and <c>refresh\n</c>; the connection is kept.</summary>
public sealed class RefreshRoute() : FixedResponseRoute(new DifferentialHttpResponse(
    200,
    [("Refresh", "0; url=/echo")],
    "refresh\n"u8.ToArray()))
{
    /// <inheritdoc/>
    public override bool Matches(DifferentialHttpRequest request) => request.Path == "/refresh";

    [ModuleInitializer]
    internal static void Register() => DifferentialRoutes.Register(new RefreshRoute());
}
