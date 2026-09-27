# Curl.Cookies.UnitLibrary

Milestone 1 (ADR-0016).

Cookie jar, Netscape cookie file format, Public Suffix List handling. Today it holds
`SetCookieParser` (Set-Cookie into `Cookie`, as curl 8.21.0 measured, reading `Expires` with
`Curl.Protocol.Abstractions`' `CurlDateParser`, ADR-0074),
`NetscapeCookieFile` (reading `-b` files
and writing the `-c` jar, byte for byte as curl 8.21.0 measured) and `CookieStore` (`ICookieStore`: which
stored cookies a request gets and in what order, `-b name=value` strings, loading files under `-j`,
saving the jar through `IFileSystem`, refusing a received cookie set on a public suffix) and the embedded
Public Suffix List snapshot (`PublicSuffixList/public_suffix_list.dat`, ADR-0049; refresh it only with
`Update-PublicSuffixList.ps1`), which `PublicSuffixList` reads as libpsl does; pin every rule to a measured curl run, never to the RFC.

Never construct a `Socket`, `SslStream` or `HttpClient` here. Take `IConnection`
so the tests in the matching `.UnitTests` project can drive this code from a
recorded byte stream with no network.
