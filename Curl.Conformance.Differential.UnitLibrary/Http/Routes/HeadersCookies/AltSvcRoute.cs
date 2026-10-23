using System.Runtime.CompilerServices;
using Curl.Conformance.Differential.Http.Core;

namespace Curl.Conformance.Differential.Http.Routes.HeadersCookies;

/// <summary>server.py's <c>/altsvc</c>: <c>Alt-Svc: h3=":443"; ma=3600</c> and <c>x</c>; the connection is kept.</summary>
public sealed class AltSvcRoute() : FixedResponseRoute(new DifferentialHttpResponse(
    200,
    [("Alt-Svc", "h3=\":443\"; ma=3600")],
    "x"u8.ToArray()))
{
    /// <inheritdoc/>
    public override bool Matches(DifferentialHttpRequest request) => request.Path == "/altsvc";

    [ModuleInitializer]
    internal static void Register() => DifferentialRoutes.Register(new AltSvcRoute());
}
