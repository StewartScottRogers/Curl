using System.Runtime.CompilerServices;
using Curl.Conformance.Differential.Http.Core;

namespace Curl.Conformance.Differential.Http.Routes.Malformed;

/// <summary>server.py's <c>/cl-and-te</c>: both <c>Content-Length</c> and chunked <c>Transfer-Encoding</c>; the connection is kept.</summary>
public sealed class ContentLengthAndTransferEncodingRoute() : FixedResponseRoute(
    "/cl-and-te",
    "HTTP/1.1 200 OK\r\nContent-Length: 3\r\nTransfer-Encoding: chunked\r\n\r\n2\r\nhi\r\n0\r\n\r\n"u8.ToArray(),
    closesConnection: false)
{
    [ModuleInitializer]
    internal static void Register() => DifferentialRoutes.Register(new ContentLengthAndTransferEncodingRoute());
}
