using System.Runtime.CompilerServices;
using Curl.Conformance.Differential.Http.Core;

namespace Curl.Conformance.Differential.Http.Routes.Malformed;

/// <summary>server.py's <c>/204-cl</c>: a 204 with a <c>Content-Length</c> and no body; the connection is kept.</summary>
public sealed class NoContentWithLengthRoute() : FixedResponseRoute(
    "/204-cl",
    "HTTP/1.1 204 No Content\r\nContent-Length: 5\r\n\r\n"u8.ToArray(),
    closesConnection: false)
{
    [ModuleInitializer]
    internal static void Register() => DifferentialRoutes.Register(new NoContentWithLengthRoute());
}
