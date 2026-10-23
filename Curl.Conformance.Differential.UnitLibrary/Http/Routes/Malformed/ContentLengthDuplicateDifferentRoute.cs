using System.Runtime.CompilerServices;
using Curl.Conformance.Differential.Http.Core;

namespace Curl.Conformance.Differential.Http.Routes.Malformed;

/// <summary>server.py's <c>/cl-dup-diff</c>: two different <c>Content-Length</c> headers, then the connection closes.</summary>
public sealed class ContentLengthDuplicateDifferentRoute() : FixedResponseRoute(
    "/cl-dup-diff",
    "HTTP/1.1 200 OK\r\nContent-Length: 1\r\nContent-Length: 2\r\n\r\nxy"u8.ToArray(),
    closesConnection: true)
{
    [ModuleInitializer]
    internal static void Register() => DifferentialRoutes.Register(new ContentLengthDuplicateDifferentRoute());
}
