using System.Runtime.CompilerServices;
using Curl.Conformance.Differential.Http.Core;

namespace Curl.Conformance.Differential.Http.Routes.HeadersCookies;

/// <summary>server.py's <c>/setcookie</c>: eight <c>Set-Cookie</c> headers (paths, <c>HttpOnly</c>, <c>Max-Age</c>, <c>Domain</c>, <c>Expires</c>, an expired one, a foreign domain, <c>Secure</c>) and <c>cookies\n</c>; the connection is kept.</summary>
public sealed class SetCookieRoute() : FixedResponseRoute(new DifferentialHttpResponse(
    200,
    [
        ("Set-Cookie", "a=1; Path=/"),
        ("Set-Cookie", "b=2; Path=/echo; HttpOnly"),
        ("Set-Cookie", "c=3; Max-Age=3600"),
        ("Set-Cookie", "d=4; Domain=127.0.0.1; Path=/"),
        ("Set-Cookie", "e=5; Expires=Thu, 01 Jan 2099 00:00:00 GMT; Path=/"),
        ("Set-Cookie", "gone=1; Max-Age=0"),
        ("Set-Cookie", "bad=1; Domain=evil.example"),
        ("Set-Cookie", "s=1; Secure; Path=/"),
    ],
    "cookies\n"u8.ToArray()))
{
    /// <inheritdoc/>
    public override bool Matches(DifferentialHttpRequest request) => request.Path == "/setcookie";

    [ModuleInitializer]
    internal static void Register() => DifferentialRoutes.Register(new SetCookieRoute());
}
