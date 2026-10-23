using System.Runtime.CompilerServices;
using Curl.Conformance.Differential.Http.Core;

namespace Curl.Conformance.Differential.Http.Routes.Malformed;

/// <summary>server.py's <c>/cl-dup-same</c>: two equal <c>Content-Length</c> headers; the connection is kept.</summary>
public sealed class ContentLengthDuplicateSameRoute() : FixedResponseRoute(
    "/cl-dup-same",
    "HTTP/1.1 200 OK\r\nContent-Length: 1\r\nContent-Length: 1\r\n\r\nx"u8.ToArray(),
    closesConnection: false)
{
    [ModuleInitializer]
    internal static void Register() => DifferentialRoutes.Register(new ContentLengthDuplicateSameRoute());
}
