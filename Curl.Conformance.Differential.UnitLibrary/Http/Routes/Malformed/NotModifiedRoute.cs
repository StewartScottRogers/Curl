using System.Runtime.CompilerServices;
using Curl.Conformance.Differential.Http.Core;

namespace Curl.Conformance.Differential.Http.Routes.Malformed;

/// <summary>server.py's <c>/notmod</c>: a plain 304 with an <c>ETag</c>; the connection is kept.</summary>
public sealed class NotModifiedRoute() : FixedResponseRoute(
    "/notmod",
    DifferentialHttpResponseWriter.Format(new DifferentialHttpResponse(304, [("ETag", "\"abc\"")], [])),
    closesConnection: false)
{
    [ModuleInitializer]
    internal static void Register() => DifferentialRoutes.Register(new NotModifiedRoute());
}
