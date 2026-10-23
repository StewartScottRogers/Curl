using System.Runtime.CompilerServices;
using Curl.Conformance.Differential.Http.Core;

namespace Curl.Conformance.Differential.Http.Routes.Malformed;

/// <summary>server.py's <c>/folded</c>: a header folded onto a continuation line; the connection is kept.</summary>
public sealed class FoldedHeaderRoute() : FixedResponseRoute(
    "/folded",
    "HTTP/1.1 200 OK\r\nDate: Thu, 01 Jan 2026 00:00:00 GMT\r\nX-F: one\r\n two\r\nContent-Length: 1\r\n\r\nx"u8.ToArray(),
    closesConnection: false)
{
    [ModuleInitializer]
    internal static void Register() => DifferentialRoutes.Register(new FoldedHeaderRoute());
}
