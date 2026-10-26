# ADR-0825: The proxy URL's user information wins over -U/--proxy-user

- Status: Accepted
- Date: 2026-10-26
- Decision: Decided by Claude under Stewart's delegation (BL-3608)
- Relates to: [ADR-0677](ADR-0677-library-handle-proxy-credentials-reach-the-proxy.md), [ADR-0024](ADR-0024-proxy-selection-follows-the-platform-curl-and-reads-an-injected-environment.md)

## Context

CGR-079: given both a proxy URL with user information and `-U`/`--proxy-user`, Curl sent the `-U`
credential; curl 8.22.0 (Schannel, Windows) sends the URL's. Measured with `Record-CurlExchange.ps1`
against `-U c:d`:

- `-x http://a:b@127.0.0.1:P` and `http_proxy=http://a:b@127.0.0.1:P`: `Proxy-Authorization: Basic YTpi` (a:b).
- `-x http://a@127.0.0.1:P`: `Basic YTo=` (a and an empty password, not `-U`'s password).
- `-x http://:b@127.0.0.1:P`: `Basic OmI=` (an empty user and b).
- `-x http://127.0.0.1:P`: `Basic Yzpk` (c:d).
- `-x socks5://a:b@...`: the SOCKS5 user/password request is `01 01 61 01 62`; `-x socks4://a@...`: the SOCKS4 USERID is `a`.

That is libcurl's `parse_proxy`: the `-U` credential is stored on the connection first and the proxy URL's
user and password, when the URL has either, replace it. The library options `CURLOPT_PROXYUSERPWD`,
`CURLOPT_PROXYUSERNAME` and `CURLOPT_PROXYPASSWORD` take the same path, so libcurl behaves the same.

## Decision

`TransferProxySelection.WithProxyUser` gives the chosen proxy the `-U` credential only when the proxy's
own `Credential` is `null`, which `ProxyUrlParser` leaves it exactly when the proxy text has no user
information (no `@`, or nothing before it). A URL with user information keeps its whole credential, both
halves, whatever `-U` gives. This holds for `-x`, the proxy environment variables and every proxy kind,
and for the library handle's options (ADR-0677), which reach the same method.

## Consequences

- ADR-0677's statement that the library options "replace the proxy URL's own user information" no longer
  holds; they apply only to a proxy URL without user information, as in libcurl.
- `Cgr079Tests` in `Curl.Engine.UnitTests` pins each measured case; `CurlCompositionProxyTests` pins the
  command-line path.
