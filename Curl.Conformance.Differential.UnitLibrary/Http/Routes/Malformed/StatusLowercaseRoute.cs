using System.Runtime.CompilerServices;
using Curl.Conformance.Differential.Http.Core;

namespace Curl.Conformance.Differential.Http.Routes.Malformed;

/// <summary>server.py's <c>/status-lower</c>: a lower-case <c>http/1.1</c> status line, then the connection closes.</summary>
public sealed class StatusLowercaseRoute() : FixedResponseRoute(
    "/status-lower",
    "http/1.1 200 OK\r\nContent-Length: 1\r\n\r\nx"u8.ToArray(),
    closesConnection: true)
{
    [ModuleInitializer]
    internal static void Register() => DifferentialRoutes.Register(new StatusLowercaseRoute());
}
