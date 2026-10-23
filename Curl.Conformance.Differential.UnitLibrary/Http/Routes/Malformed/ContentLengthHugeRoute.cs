using System.Runtime.CompilerServices;
using Curl.Conformance.Differential.Http.Core;

namespace Curl.Conformance.Differential.Http.Routes.Malformed;

/// <summary>server.py's <c>/cl-huge</c>: a <c>Content-Length</c> past 64 bits, then the connection closes.</summary>
public sealed class ContentLengthHugeRoute() : FixedResponseRoute(
    "/cl-huge",
    "HTTP/1.1 200 OK\r\nContent-Length: 99999999999999999999\r\n\r\nx"u8.ToArray(),
    closesConnection: true)
{
    [ModuleInitializer]
    internal static void Register() => DifferentialRoutes.Register(new ContentLengthHugeRoute());
}
