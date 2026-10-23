using System.Runtime.CompilerServices;
using Curl.Conformance.Differential.Http.Core;

namespace Curl.Conformance.Differential.Http.Routes.Malformed;

/// <summary>server.py's <c>/status-ver2</c>: an <c>HTTP/2.0</c> status line over HTTP/1.1; the connection is kept.</summary>
public sealed class StatusVersion2Route() : FixedResponseRoute(
    "/status-ver2",
    "HTTP/2.0 200 OK\r\nContent-Length: 1\r\n\r\nx"u8.ToArray(),
    closesConnection: false)
{
    [ModuleInitializer]
    internal static void Register() => DifferentialRoutes.Register(new StatusVersion2Route());
}
