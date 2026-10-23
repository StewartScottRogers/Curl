using System.Runtime.CompilerServices;
using Curl.Conformance.Differential.Http.Core;

namespace Curl.Conformance.Differential.Http.Routes.Malformed;

/// <summary>server.py's <c>/badstatus</c>: a status line that is not HTTP, then the connection closes.</summary>
public sealed class BadStatusRoute() : FixedResponseRoute(
    "/badstatus",
    "garbage\r\n\r\n"u8.ToArray(),
    closesConnection: true)
{
    [ModuleInitializer]
    internal static void Register() => DifferentialRoutes.Register(new BadStatusRoute());
}
