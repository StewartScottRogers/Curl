using System.Runtime.CompilerServices;
using Curl.Conformance.Differential.Http.Core;

namespace Curl.Conformance.Differential.Http.Routes.HeadersCookies;

/// <summary>server.py's <c>/hugehdr</c>: one <c>X-Big</c> header of 400000 <c>v</c>, and <c>x</c>; the connection is kept.</summary>
public sealed class HugeHeaderRoute() : FixedResponseRoute(new DifferentialHttpResponse(
    200,
    [("X-Big", new string('v', 400000))],
    "x"u8.ToArray()))
{
    /// <inheritdoc/>
    public override bool Matches(DifferentialHttpRequest request) => request.Path == "/hugehdr";

    [ModuleInitializer]
    internal static void Register() => DifferentialRoutes.Register(new HugeHeaderRoute());
}
