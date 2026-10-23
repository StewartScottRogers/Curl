using System.Runtime.CompilerServices;
using Curl.Conformance.Differential.Http.Core;

namespace Curl.Conformance.Differential.Http.Routes.Malformed;

/// <summary>server.py's <c>/status-ver9</c>: an <c>HTTP/1.9</c> status line, then the connection closes.</summary>
public sealed class StatusVersion9Route() : FixedResponseRoute(
    "/status-ver9",
    "HTTP/1.9 200 OK\r\nContent-Length: 1\r\n\r\nx"u8.ToArray(),
    closesConnection: true)
{
    [ModuleInitializer]
    internal static void Register() => DifferentialRoutes.Register(new StatusVersion9Route());
}
