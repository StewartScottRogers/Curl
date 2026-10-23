using System.Runtime.CompilerServices;
using Curl.Conformance.Differential.Http.Core;

namespace Curl.Conformance.Differential.Http.Routes.Malformed;

/// <summary>server.py's <c>/hdr-nospace</c>: headers with no space, or a tab, after the colon; the connection is kept.</summary>
public sealed class HeaderNoSpaceRoute() : FixedResponseRoute(
    "/hdr-nospace",
    "HTTP/1.1 200 OK\r\nDate: Thu, 01 Jan 2026 00:00:00 GMT\r\nX-NoSpace:val\r\nX-Tab:\tv\r\nContent-Length: 1\r\n\r\nx"u8.ToArray(),
    closesConnection: false)
{
    [ModuleInitializer]
    internal static void Register() => DifferentialRoutes.Register(new HeaderNoSpaceRoute());
}
