# Curl.Cookies.UnitLibrary

Milestone 1 (ADR-0016).

Cookie jar, Netscape cookie file format, Public Suffix List handling. Today it holds
`SetCookieParser` (Set-Cookie into `Cookie`, as curl 8.21.0 measured),
`CookieDateParser` (libcurl's `parsedate`, for `Expires`) and `CookieStore` (`ICookieStore`: which
stored cookies a request gets and in what order, as curl 8.21.0 measured); pin every rule to a
measured curl run, never to the RFC.

Never construct a `Socket`, `SslStream` or `HttpClient` here. Take `IConnection`
so the tests in the matching `.UnitTests` project can drive this code from a
recorded byte stream with no network.
