using System.Runtime.CompilerServices;
using Curl.Conformance.Differential.Http.Core;

namespace Curl.Conformance.Differential.Http.Routes.Bodies;

/// <summary>server.py's <c>/utf8</c>: <c>héllo wörld ✓\n</c> UTF-8 encoded, as <c>text/plain; charset=utf-8</c>; the connection is kept.</summary>
public sealed class Utf8Route : FixedPathRoute
{
    /// <inheritdoc/>
    public override string Path => "/utf8";

    /// <inheritdoc/>
    protected override DifferentialHttpResponse BuildResponse(DifferentialHttpRequest request) =>
        new(200, [("Content-Type", "text/plain; charset=utf-8")], "héllo wörld ✓\n"u8.ToArray());

    [ModuleInitializer]
    internal static void Register() => DifferentialRoutes.Register(new Utf8Route());
}
