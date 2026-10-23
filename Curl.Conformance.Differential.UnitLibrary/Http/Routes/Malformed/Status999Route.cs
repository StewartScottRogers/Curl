using System.Runtime.CompilerServices;
using Curl.Conformance.Differential.Http.Core;

namespace Curl.Conformance.Differential.Http.Routes.Malformed;

/// <summary>server.py's <c>/status-999</c>: status 999; the connection is kept.</summary>
public sealed class Status999Route() : FixedResponseRoute(
    "/status-999",
    "HTTP/1.1 999 Odd\r\nContent-Length: 1\r\n\r\nx"u8.ToArray(),
    closesConnection: false)
{
    [ModuleInitializer]
    internal static void Register() => DifferentialRoutes.Register(new Status999Route());
}
