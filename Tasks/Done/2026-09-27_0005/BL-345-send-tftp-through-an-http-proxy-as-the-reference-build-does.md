---
id: BL-345
title: Send tftp:// through an HTTP proxy as the reference build does and fail with exit 7
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-337]
touches: [Curl.Protocol.Tftp.UnitLibrary, Curl.Protocol.Tftp.UnitTests]
requirement: none
created: 2026-09-27
completed: 2026-09-27
---
# BL-345 — Send tftp:// through an HTTP proxy as the reference build does and fail with exit 7

## Goal

`curl -x http://<proxy> tftp://…` sends the reference build's MASQUE request to the proxy and ends with exit 7 `bind() failed; Invalid arguments`, never sending TFTP datagrams around the proxy.

## Context

- ADR-0056, rule 4.
- Measured by BL-330: `curl -sS -x http://127.0.0.1:18331 tftp://example.com/f` sent `GET http://127.0.0.1:18331/.well-known/masque/udp/example.com/69/ HTTP/1.1\r\nHost: 127.0.0.1:18331\r\nUser-Agent: curl/8.21.0\r\nProxy-Connection: Keep-Alive\r\nConnection: Upgrade\r\nUpgrade: connect-udp\r\nCapsule-Protocol: ?1\r\n\r\n`, then wrote `curl: (7) bind() failed; Invalid arguments` to stderr and exited 7.
- `TftpProtocolHandler` takes only an `IDatagramConnector`; sending the request needs a TCP `IConnector` too. If that widens the task, split it rather than grow it.
- Where a criterion says *measured*, the bytes were measured by BL-330 with curl 8.21.0 (`/mingw64/bin/curl`, ADR-0009) against a loopback listener and are recorded under BL-330's `Notes`. Pin only those bytes; re-measure with `Record-CurlExchange.ps1` for anything else.

## Acceptance criteria

- [x] A test shows a context with an HTTP `Proxy` sends no datagram and returns exit 7 with `bind() failed; Invalid arguments`.
- [x] A test pins the measured MASQUE request bytes sent to the proxy.
- [x] A context without a proxy behaves as before; 100% line and branch coverage of the changed code.

## Notes

- Pipeline `feature`, delivered in-session: one handler branch, one request builder and their tests inside one library, with ADR-0056 rule 4 already holding the design, so the architect, implementer and reviewer stages were collapsed (sensible default for a one-library change).
- Design: `TftpProtocolHandler(IDatagramConnector connector, IConnector? proxyConnector = null, Encoding? proxyCredentialEncoding = null)`. The optional parameters keep `Curl.Console`'s composition compiling unchanged; `Curl.Console` is in another lane's `touches`, so wiring the connector there is BL-397. Without a proxy connector, an HTTP proxy still ends in exit 7 `bind() failed; Invalid arguments` and sends nothing, so no datagram ever goes around the proxy. The handler connects to the proxy itself (`ConnectTarget(proxy.Host, proxy.Port, UseTls: false)` with no `Proxy`, so the connector opens no CONNECT tunnel), writes the request, and does not read the reply (curl fails the same whether or not the proxy answers). A proxy that cannot be reached returns the connector's code and message unchanged, as a datagram channel that will not open already does. The credential encoding defaults to UTF-8; the composition should pass the platform's (ADR-0059).
- New measurements (curl 8.21.0, `Record-CurlExchange.ps1`, 2026-09-27), all exit 7 `curl: (7) bind() failed; Invalid arguments`: `--proxy1.0 127.0.0.1:18345 tftp://example.com/f` sends the same request with `HTTP/1.0`; `-x http://127.0.0.1:18345 -U u:p -A X/1 tftp://[::1]:70/f` sends `GET http://127.0.0.1:18345/.well-known/masque/udp/%3A%3A1/70/ HTTP/1.1`, `Host`, `Proxy-Authorization: Basic dTpw`, `User-Agent: X/1`, then the four fixed headers; `tftp://example.com/` (no file name) and `-T file` send the same 221-byte request as the plain download, so the proxy check comes before `Missing filename`.
- Not measured, so not pinned: `--proxy-header` on the MASQUE request (not applied), and an empty `-A` (sends no `User-Agent`, following `HttpRequestOptions.UserAgent`'s rule). SOCKS and HTTPS proxy kinds are unchanged (direct datagrams): filed as BL-398.
- Verified 2026-09-27: `dotnet build` 0 warnings 0 errors; fast tests green in all 16 test assemblies (Curl.Protocol.Tftp.UnitTests 99 passed); `Measure-CodeQuality.ps1 -Library Curl.Protocol.Tftp.UnitLibrary` 100% line and branch, 0 failing members, worst CRAP 10 (`ExecuteAsync` split into `TransferFileAsync` to stay within complexity 10); `dotnet format --verify-no-changes` clean on both projects.

## Log

- 2026-09-27: Created.
- 2026-09-27: Backlog -> Doing.
- 2026-09-27: Doing -> Done. tftp:// through an HTTP proxy sends the measured MASQUE request to the proxy and fails with exit 7 bind() failed; Invalid arguments, sending no datagram
