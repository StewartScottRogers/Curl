using System.Runtime.CompilerServices;
using Curl.Conformance.Differential.Http.Core;

namespace Curl.Conformance.Differential.Http.Routes.Malformed;

/// <summary>server.py's <c>/head-body</c>: a body even to <c>HEAD</c>, then the connection closes.</summary>
public sealed class HeadBodyRoute() : FixedResponseRoute(
    "/head-body",
    "HTTP/1.1 200 OK\r\nContent-Length: 5\r\n\r\nhello"u8.ToArray(),
    closesConnection: true)
{
    [ModuleInitializer]
    internal static void Register() => DifferentialRoutes.Register(new HeadBodyRoute());
}
