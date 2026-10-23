using System.Runtime.CompilerServices;
using Curl.Conformance.Differential.Http.Core;

namespace Curl.Conformance.Differential.Http.Routes.Malformed;

/// <summary>server.py's <c>/hdr-space-before-colon</c>: a header with a space before its colon; the connection is kept.</summary>
public sealed class HeaderSpaceBeforeColonRoute() : FixedResponseRoute(
    "/hdr-space-before-colon",
    "HTTP/1.1 200 OK\r\nDate: Thu, 01 Jan 2026 00:00:00 GMT\r\nX-Sp : v\r\nContent-Length: 1\r\n\r\nx"u8.ToArray(),
    closesConnection: false)
{
    [ModuleInitializer]
    internal static void Register() => DifferentialRoutes.Register(new HeaderSpaceBeforeColonRoute());
}
