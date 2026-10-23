using System.Runtime.CompilerServices;
using Curl.Conformance.Differential.Http.Core;

namespace Curl.Conformance.Differential.Http.Routes.Malformed;

/// <summary>server.py's <c>/http10len</c>: an HTTP/1.0 answer with a <c>Content-Length</c>, then the connection closes.</summary>
public sealed class Http10LengthRoute() : FixedResponseRoute(
    "/http10len",
    DifferentialHttpResponseWriter.Format(new DifferentialHttpResponse(200, [("Content-Length", "5")], "abcde"u8.ToArray(), "HTTP/1.0")),
    closesConnection: true)
{
    [ModuleInitializer]
    internal static void Register() => DifferentialRoutes.Register(new Http10LengthRoute());
}
