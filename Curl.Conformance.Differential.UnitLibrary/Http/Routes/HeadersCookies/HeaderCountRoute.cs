using System.Globalization;
using System.Runtime.CompilerServices;
using Curl.Conformance.Differential.Http.Core;

namespace Curl.Conformance.Differential.Http.Routes.HeadersCookies;

/// <summary>server.py's <c>/hdr-count</c>: 5000 headers <c>X-0: 1</c> to <c>X-4999: 1</c>, and <c>x</c>; the connection is kept.</summary>
public sealed class HeaderCountRoute() : FixedResponseRoute(new DifferentialHttpResponse(
    200,
    [.. Enumerable.Range(0, 5000).Select(index => (string.Create(CultureInfo.InvariantCulture, $"X-{index}"), "1"))],
    "x"u8.ToArray()))
{
    /// <inheritdoc/>
    public override bool Matches(DifferentialHttpRequest request) => request.Path == "/hdr-count";

    [ModuleInitializer]
    internal static void Register() => DifferentialRoutes.Register(new HeaderCountRoute());
}
