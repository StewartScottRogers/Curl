using System.Runtime.CompilerServices;
using Curl.Conformance.Differential.Http.Core;

namespace Curl.Conformance.Differential.Http.Routes.Malformed;

/// <summary>server.py's <c>/lenshort</c>: a body shorter than its <c>Content-Length</c>, then the connection closes.</summary>
public sealed class LengthShortRoute() : FixedResponseRoute(
    "/lenshort",
    "HTTP/1.1 200 OK\r\nDate: Thu, 01 Jan 2026 00:00:00 GMT\r\nContent-Length: 100\r\n\r\nshort"u8.ToArray(),
    closesConnection: true)
{
    [ModuleInitializer]
    internal static void Register() => DifferentialRoutes.Register(new LengthShortRoute());
}
