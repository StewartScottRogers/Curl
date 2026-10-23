using System.Runtime.CompilerServices;
using Curl.Conformance.Differential.Http.Core;

namespace Curl.Conformance.Differential.Http.Routes.Malformed;

/// <summary>server.py's <c>/status-nocode</c>: a status line with no code, then the connection closes.</summary>
public sealed class StatusNoCodeRoute() : FixedResponseRoute(
    "/status-nocode",
    "HTTP/1.1 \r\nContent-Length: 1\r\n\r\nx"u8.ToArray(),
    closesConnection: true)
{
    [ModuleInitializer]
    internal static void Register() => DifferentialRoutes.Register(new StatusNoCodeRoute());
}
