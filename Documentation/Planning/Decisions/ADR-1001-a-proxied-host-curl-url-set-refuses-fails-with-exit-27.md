# ADR-1001: A proxied host `curl_url_set` refuses fails with exit 27 once the proxy connection is open

- Status: Accepted
- Date: 2026-10-29
- Decided by Claude under Stewart's delegation (BL-4080)

## Context

A UTS 46 mapping can leave a character in an ASCII host that curl's URL parser would refuse: `a℀b.test` is `aa/cb.test` (ADR-1000, BL-4081). Measured with curl 8.18.0 and libidn2 2.3.8 on WSL Ubuntu (BL-4077): a direct transfer sends `Host: aa/cb.test`, but through an HTTP proxy, `curl -x http://127.0.0.1:PORT http://a℀b.test/` exits 27 (`curl: (27) Out of memory`) and the proxy receives no byte. With the proxy port closed it exits 7, so the failure comes after the proxy connection opens: `http_target` builds the absolute-form target with `curl_url_set(CURLUPART_HOST)`, whose `hostname_check` refuses the host, and answers `CURLE_OUT_OF_MEMORY`.

## Decision

`HttpProtocolHandler.ExchangeOnConnectionAsync` fails a request forwarded to an HTTP proxy in absolute form, before writing any byte, with exit 27 and `Out of memory` when the wire host (`HttpUrlText.WireHost`) holds any character of `hostname_check`'s set (`` \r\n\t/:#?!@{}[]\$'"^`*<>=;,+&()%``). An IPv6 literal, the only host with a `:`, is never refused, since curl parses it apart. The whole set is used, not only the slash that was measured, because curl refuses each of them through the same call; `a,b.test` (U+FE10) therefore fails the same way. CONNECT tunnels are left as they are, since nothing was measured for them.

## Consequences

On Windows, `CurlUrlHost` converts a non-ASCII host from its bytes read as Windows-1252, which leaves no such character, so the path is reached only off Windows. Its end-to-end test runs only there. BL-4090 makes it covered on Windows.
