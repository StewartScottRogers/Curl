using System.Runtime.CompilerServices;
using Curl.Conformance.Differential.Http.Core;

namespace Curl.Conformance.Differential.Http.Routes.Malformed;

/// <summary>server.py's <c>/nothing</c>: no answer at all; the connection closes.</summary>
public sealed class NothingRoute() : FixedResponseRoute(
    "/nothing",
    [],
    closesConnection: true)
{
    [ModuleInitializer]
    internal static void Register() => DifferentialRoutes.Register(new NothingRoute());
}
