using System.Runtime.CompilerServices;
using Curl.Conformance.Differential.Http.Core;

namespace Curl.Conformance.Differential.Http.Routes.Malformed;

/// <summary>server.py's <c>/nocontent</c>: a plain 204 with no length; the connection is kept.</summary>
public sealed class NoContentRoute() : FixedResponseRoute(
    "/nocontent",
    DifferentialHttpResponseWriter.Format(new DifferentialHttpResponse(204, [], [])),
    closesConnection: false)
{
    [ModuleInitializer]
    internal static void Register() => DifferentialRoutes.Register(new NoContentRoute());
}
