using System.Runtime.CompilerServices;
using Curl.Conformance.Differential.Http.Core;

namespace Curl.Conformance.Differential.Http.Routes.Malformed;

/// <summary>server.py's <c>/status-noreason</c>: a status line with no reason phrase; the connection is kept.</summary>
public sealed class StatusNoReasonRoute() : FixedResponseRoute(
    "/status-noreason",
    "HTTP/1.1 200\r\nContent-Length: 1\r\n\r\nx"u8.ToArray(),
    closesConnection: false)
{
    [ModuleInitializer]
    internal static void Register() => DifferentialRoutes.Register(new StatusNoReasonRoute());
}
