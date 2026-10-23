using System.Runtime.CompilerServices;
using Curl.Conformance.Differential.Http.Core;

namespace Curl.Conformance.Differential.Http.Routes.Malformed;

/// <summary>server.py's <c>/http10</c>: an HTTP/1.0 answer with no length, ended by the close.</summary>
public sealed class Http10Route() : FixedResponseRoute(
    "/http10",
    DifferentialHttpResponseWriter.Format(new DifferentialHttpResponse(200, [], "http10 body"u8.ToArray(), "HTTP/1.0", AddsContentLength: false)),
    closesConnection: true)
{
    [ModuleInitializer]
    internal static void Register() => DifferentialRoutes.Register(new Http10Route());
}
