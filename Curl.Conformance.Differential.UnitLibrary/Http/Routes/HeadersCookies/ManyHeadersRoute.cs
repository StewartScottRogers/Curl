using System.Globalization;
using System.Runtime.CompilerServices;
using Curl.Conformance.Differential.Http.Core;

namespace Curl.Conformance.Differential.Http.Routes.HeadersCookies;

/// <summary>server.py's <c>/manyhdrs</c>: 300 headers <c>X-H0</c> to <c>X-H299</c> of 50 <c>v</c> each, and <c>x</c>; the connection is kept.</summary>
public sealed class ManyHeadersRoute() : FixedResponseRoute(new DifferentialHttpResponse(
    200,
    [.. Enumerable.Range(0, 300).Select(index => (string.Create(CultureInfo.InvariantCulture, $"X-H{index}"), new string('v', 50)))],
    "x"u8.ToArray()))
{
    /// <inheritdoc/>
    public override bool Matches(DifferentialHttpRequest request) => request.Path == "/manyhdrs";

    [ModuleInitializer]
    internal static void Register() => DifferentialRoutes.Register(new ManyHeadersRoute());
}
