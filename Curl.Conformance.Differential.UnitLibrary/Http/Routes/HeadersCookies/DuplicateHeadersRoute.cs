using System.Runtime.CompilerServices;
using Curl.Conformance.Differential.Http.Core;

namespace Curl.Conformance.Differential.Http.Routes.HeadersCookies;

/// <summary>server.py's <c>/dupheaders</c>: <c>X-A</c> twice, <c>X-B</c> with its padding kept, an empty <c>X-Empty</c>, and <c>x</c>; the connection is kept.</summary>
public sealed class DuplicateHeadersRoute() : FixedResponseRoute(new DifferentialHttpResponse(
    200,
    [("X-A", "1"), ("X-A", "2"), ("X-B", "  spaced  "), ("X-Empty", string.Empty)],
    "x"u8.ToArray()))
{
    /// <inheritdoc/>
    public override bool Matches(DifferentialHttpRequest request) => request.Path == "/dupheaders";

    [ModuleInitializer]
    internal static void Register() => DifferentialRoutes.Register(new DuplicateHeadersRoute());
}
