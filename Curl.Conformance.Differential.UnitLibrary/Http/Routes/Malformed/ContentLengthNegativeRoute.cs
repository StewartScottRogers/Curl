using System.Runtime.CompilerServices;
using Curl.Conformance.Differential.Http.Core;

namespace Curl.Conformance.Differential.Http.Routes.Malformed;

/// <summary>server.py's <c>/cl-neg</c>: a negative <c>Content-Length</c>, then the connection closes.</summary>
public sealed class ContentLengthNegativeRoute() : FixedResponseRoute(
    "/cl-neg",
    "HTTP/1.1 200 OK\r\nContent-Length: -5\r\n\r\nx"u8.ToArray(),
    closesConnection: true)
{
    [ModuleInitializer]
    internal static void Register() => DifferentialRoutes.Register(new ContentLengthNegativeRoute());
}
