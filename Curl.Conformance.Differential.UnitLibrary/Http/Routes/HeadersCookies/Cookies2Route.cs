using System.Runtime.CompilerServices;
using Curl.Conformance.Differential.Http.Core;

namespace Curl.Conformance.Differential.Http.Routes.HeadersCookies;

/// <summary>server.py's <c>/cookies2</c>: seventeen odd <c>Set-Cookie</c> headers (quoted and spaced values, prefixed names, a public suffix domain, no value, no name, a 5000-byte value, past and far-future expiry, duplicates, a foreign path, and <c>uni=é</c>, which goes out UTF-8 encoded as server.py's <c>str.encode()</c> sends it) and <c>c2\n</c>; the connection is kept.</summary>
public sealed class Cookies2Route() : FixedResponseRoute(new DifferentialHttpResponse(
    200,
    [
        ("Set-Cookie", "q=\"quoted val\"; Path=/"),
        ("Set-Cookie", "sp=a b c; Path=/"),
        ("Set-Cookie", "__Secure-x=1; Path=/"),
        ("Set-Cookie", "__Host-y=1; Path=/; Secure"),
        ("Set-Cookie", "tld=1; Domain=.com"),
        ("Set-Cookie", "ip=1; Domain=127.0.0.1"),
        ("Set-Cookie", "ss=1; SameSite=Strict; Path=/"),
        ("Set-Cookie", "noval; Path=/"),
        ("Set-Cookie", "=emptyname; Path=/"),
        ("Set-Cookie", "big=" + new string('v', 5000) + "; Path=/"),
        ("Set-Cookie", "exp=1; Expires=Wed, 09 Jun 2021 10:18:14 GMT; Path=/"),
        ("Set-Cookie", "future=1; Expires=Fri, 31 Dec 9999 23:59:59 GMT; Path=/"),
        ("Set-Cookie", "maxage=1; Max-Age=60; Expires=Wed, 09 Jun 2021 10:18:14 GMT; Path=/"),
        ("Set-Cookie", "dup=1; Path=/"),
        ("Set-Cookie", "dup=2; Path=/"),
        ("Set-Cookie", "pathx=1; Path=/other"),
        ("Set-Cookie", "uni=é; Path=/"),
    ],
    "c2\n"u8.ToArray()))
{
    /// <inheritdoc/>
    public override bool Matches(DifferentialHttpRequest request) => request.Path == "/cookies2";

    [ModuleInitializer]
    internal static void Register() => DifferentialRoutes.Register(new Cookies2Route());
}
