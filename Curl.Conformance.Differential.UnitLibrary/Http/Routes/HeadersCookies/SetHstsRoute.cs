using System.Runtime.CompilerServices;
using Curl.Conformance.Differential.Http.Core;

namespace Curl.Conformance.Differential.Http.Routes.HeadersCookies;

/// <summary>server.py's <c>/sethsts</c>: <c>Strict-Transport-Security: max-age=31536000; includeSubDomains</c> and <c>h\n</c>; the connection is kept.</summary>
public sealed class SetHstsRoute() : FixedResponseRoute(new DifferentialHttpResponse(
    200,
    [("Strict-Transport-Security", "max-age=31536000; includeSubDomains")],
    "h\n"u8.ToArray()))
{
    /// <inheritdoc/>
    public override bool Matches(DifferentialHttpRequest request) => request.Path == "/sethsts";

    [ModuleInitializer]
    internal static void Register() => DifferentialRoutes.Register(new SetHstsRoute());
}
