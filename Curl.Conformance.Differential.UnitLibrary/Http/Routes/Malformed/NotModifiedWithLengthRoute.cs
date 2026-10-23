using System.Runtime.CompilerServices;
using Curl.Conformance.Differential.Http.Core;

namespace Curl.Conformance.Differential.Http.Routes.Malformed;

/// <summary>server.py's <c>/304-body</c>: a 304 with a <c>Content-Length</c> and no body; the connection is kept.</summary>
public sealed class NotModifiedWithLengthRoute() : FixedResponseRoute(
    "/304-body",
    "HTTP/1.1 304 Not Modified\r\nContent-Length: 5\r\n\r\n"u8.ToArray(),
    closesConnection: false)
{
    [ModuleInitializer]
    internal static void Register() => DifferentialRoutes.Register(new NotModifiedWithLengthRoute());
}
