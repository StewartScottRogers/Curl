using System.Runtime.CompilerServices;
using Curl.Conformance.Differential.Http.Core;

namespace Curl.Conformance.Differential.Http.Routes.Malformed;

/// <summary>server.py's <c>/status-2digit</c>: a two-digit status code, then the connection closes.</summary>
public sealed class StatusTwoDigitRoute() : FixedResponseRoute(
    "/status-2digit",
    "HTTP/1.1 20 OK\r\nContent-Length: 1\r\n\r\nx"u8.ToArray(),
    closesConnection: true)
{
    [ModuleInitializer]
    internal static void Register() => DifferentialRoutes.Register(new StatusTwoDigitRoute());
}
