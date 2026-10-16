# ADR-0551 — `CURLINFO_PROXY_ERROR` is the failed SOCKS handshake's `CURLPX_*` code, and `-w` has no `%{proxy_error}`

- **Status:** Accepted
- **Date:** 2026-10-15

**Decided by Claude under Stewart's delegation** (root `CLAUDE.md`, "Decisions"), for BL-2375.

## Context

`TransferInfo.ProxyError` (`Curl.Api.UnitLibrary`) answered 0 after every transfer. libcurl's
`CURLINFO_PROXY_ERROR` is `data->info.pxcode`, which upstream's `lib/socks.c` sets to a `CURLPX_*`
code when a SOCKS4, SOCKS4a, SOCKS5 or SOCKS5h handshake fails and the transfer ends with exit 97.

BL-2375 also asked for a `%{proxy_error}` write-out variable. Measured with `Record-CurlExchange.ps1`
against a scripted SOCKS server, curl 8.22.0 (Schannel build) has none: `-w "[%{proxy_error}]"` wrote
`[]` and `curl: unknown --write-out variable: 'proxy_error'` through a SOCKS5 proxy answering reply 5
(exit 97), through a SOCKS4 proxy answering 91 (exit 97), and with no proxy (exit 7).

## Decision

1. `Curl.Protocol.Abstractions` gains `CurlProxyCode`, upstream's `CURLproxycode` with its numbers.
   `ConnectResult.ProxyError` carries it (set through `ConnectResult.Failed`'s `proxyError`), and
   `TransferReport.ProxyError` copies it.
2. `Curl.Networking`'s handshakes give each failure the code upstream's `socks.c` gives it: too long a
   user, password or host name `LongUser`, `LongPassword`, `LongHostname`; a closed connection
   `Closed`; a wrong version `BadVersion`; SOCKS4 replies 91, 92, 93 and others `RequestFailed`,
   `Identd`, `IdentdDiffer`, `UnknownFail`; a SOCKS4 target with no IPv4 address `ResolveHost`; a
   SOCKS5 method that is not acceptable or not enabled `NoAuthentication` (GSS-API not enabled
   `GssapiPerMessage`), an unknown one `UnknownMode`; any GSS-API negotiation failure `Gssapi`; a
   rejected user `UserRejected`; SOCKS5 replies 1 to 8 their `Reply*` names and anything above
   `ReplyUnassigned`; an unknown bound address type `BadAddressType`.
3. A host the SOCKS4 or SOCKS5 local resolve cannot find keeps `Ok`: curl 8.22.0 ends it with exit 6
   (`Could not resolve host`), not exit 97, so the resolver's code, not a proxy code, describes it.
4. `Curl.Engine`'s `ConnectionEndPointRecorder` keeps the last failed connect's code, as it keeps the
   OS errno (ADR-0550), and `TransferInfoFromReport` fills `TransferInfo.ProxyError` from the report.
5. No `%{proxy_error}` variable is added and `--ai-help` lists none: Curl prints curl 8.22.0's
   unknown-variable warning for it, as for any name curl does not know, so it stays a drop-in
   replacement.

## Consequences

`Libcurl`'s `CURLINFO_PROXY_ERROR` answers the code after a failed SOCKS handshake. Failures thrown
while sending or reading (an `IOException`) still escape as exceptions rather than as
`SendConnect` or `ReceiveConnect` results, as they did before.
