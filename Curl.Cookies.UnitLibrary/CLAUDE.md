# Curl.Cookies.UnitLibrary

Phase 2.

Cookie jar, Netscape cookie file format, Public Suffix List handling.

Never construct a `Socket`, `SslStream` or `HttpClient` here. Take `IConnection`
so the tests in the matching `.UnitTests` project can drive this code from a
recorded byte stream with no network.
