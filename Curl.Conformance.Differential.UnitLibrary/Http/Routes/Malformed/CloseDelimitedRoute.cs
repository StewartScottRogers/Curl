using System.Runtime.CompilerServices;
using Curl.Conformance.Differential.Http.Core;

namespace Curl.Conformance.Differential.Http.Routes.Malformed;

/// <summary>server.py's <c>/closedelim</c>: a body with no length, ended by <c>Connection: close</c>.</summary>
public sealed class CloseDelimitedRoute() : FixedResponseRoute(
    "/closedelim",
    DifferentialHttpResponseWriter.Format(new DifferentialHttpResponse(200, [("Content-Type", "text/plain")], "until close\n"u8.ToArray(), AddsContentLength: false, AddsConnectionClose: true)),
    closesConnection: true)
{
    [ModuleInitializer]
    internal static void Register() => DifferentialRoutes.Register(new CloseDelimitedRoute());
}
