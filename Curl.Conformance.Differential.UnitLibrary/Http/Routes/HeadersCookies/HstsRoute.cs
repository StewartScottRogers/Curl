using System.Runtime.CompilerServices;
using Curl.Conformance.Differential.Http.Core;

namespace Curl.Conformance.Differential.Http.Routes.HeadersCookies;

/// <summary>server.py's <c>/hsts</c>: <c>Strict-Transport-Security: max-age=3600</c> and <c>x</c>; the connection is kept.</summary>
public sealed class HstsRoute() : FixedResponseRoute(new DifferentialHttpResponse(
    200,
    [("Strict-Transport-Security", "max-age=3600")],
    "x"u8.ToArray()))
{
    /// <inheritdoc/>
    public override bool Matches(DifferentialHttpRequest request) => request.Path == "/hsts";

    [ModuleInitializer]
    internal static void Register() => DifferentialRoutes.Register(new HstsRoute());
}
