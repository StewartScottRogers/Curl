# ADR-0058 — `%{proxy_used}` is reported by the HTTP handler when the transfer is set to go through a proxy

- **Status:** Accepted
- **Date:** 2026-09-27

Decided by Claude under Stewart's delegation (task BL-302, 2026-09-27).

## Context

ADR-0043 left `%{proxy_used}` unknown because nothing recorded whether a transfer went
through a proxy, and ADR-0015 said a later ADR would add the `TransferReport` member for it.
BL-302 adds it.

Measured on 2026-09-27 with the Windows reference build (`/mingw64/bin/curl`, curl 8.21.0,
Schannel) against a loopback origin on 18081 and a loopback proxy on 18080 that forwards and
answers CONNECT. The commands are in the BL-302 Notes.

| Transfer | `%{proxy_used}` |
| --- | --- |
| `http://127.0.0.1:18081/`, no proxy | `0` |
| `-x http://127.0.0.1:18080 http://example.test/`, forwarded | `1` |
| `-p -x http://127.0.0.1:18080 http://example.test/`, CONNECT tunnel | `1` |
| `-x http://127.0.0.1:18080 --noproxy '*' http://127.0.0.1:18081/` | `0` |
| `-x http://127.0.0.1:1 http://example.test/`, connect refused, exit 7 | `1` |
| `-x socks5://127.0.0.1:1 http://example.test/`, connect refused, exit 7 | `1` |
| `file://…`, with or without `-x` | `0` |

So curl reports the proxy the transfer was set to use, not whether it answered.

## Decision

1. `TransferReport.UsedProxy` (`bool`, default `false`) is the source of `%{proxy_used}`,
   which `TransferWriteOutVariables` prints as `1` or `0`; a result without a report prints
   `0`.
2. The HTTP handler sets it when `HttpRequestOptions.ForwardProxy` is not `null`: forwarded
   or tunnelled, HTTP or SOCKS, on every report it builds. `Curl.Console`'s proxy selection
   already leaves `ForwardProxy` `null` for a `--noproxy` match and for `file://`.
3. When connecting fails and a proxy was selected, the handler's failure carries a report
   holding only `UsedProxy = true`, so a refused proxy prints `1` as curl does. A direct
   connect failure still carries no report.
4. Other handlers leave it `false`. The ones that tunnel through a proxy (ADR-0056) set it
   when a task measures them; `file://` never uses one.

## Consequences

- `-w "%{proxy_used}"` matches curl for HTTP transfers, direct, forwarded, tunnelled and
  refused.
- A non-HTTP scheme tunnelled through a proxy prints `0` until its handler sets the member.

## Alternatives considered

- **Set it from `ConnectResult`.** The connector would report it only on success, and a
  forwarded request connects to the proxy as a plain target, so the connector cannot tell.
- **Set it in `Curl.Console`, which chose the proxy.** It would need to rewrite every
  handler's report; the handler knows its route and is where ADR-0015 puts such facts.
