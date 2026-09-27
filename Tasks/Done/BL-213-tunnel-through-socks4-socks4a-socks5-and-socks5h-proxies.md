---
id: BL-213
title: Tunnel through SOCKS4, SOCKS4a, SOCKS5 and SOCKS5h proxies
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-162]
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests]
requirement: none
created: 2026-09-26
completed: 2026-09-27
---
# BL-213 — Tunnel through SOCKS4, SOCKS4a, SOCKS5 and SOCKS5h proxies

## Goal

The connector performs SOCKS4, SOCKS4a, SOCKS5 and SOCKS5h handshakes for a SOCKS `ConnectTarget.Proxy`.

## Context

- Filed from the Phase 1 HTTP plan (protocol-architect, 2026-09-26), item N3. Upstream references: https://curl.se/docs/manpage.html and https://curl.se/libcurl/c/libcurl-errors.html; behaviour measured on curl 8.21.0.
- RFC 1928/1929 and the SOCKS4/4a specifications.
- Where a criterion says *measured*, run curl 8.21.0 - the mingw build `/mingw64/bin/curl`, first on PATH, which is the Windows reference (ADR-0009 and the BL-153 ADR) - against a loopback server (the BL-165 recording script `Record-CurlExchange.ps1` once it exists), record the exact command and the bytes it produced in `Notes`, then pin them in a test. Never pin text that was not measured.

## Acceptance criteria

- [x] Handshake bytes are scripted per version and match curl 8.21.0 (measured).
- [x] Failures return `CurlExitCode.Proxy` (97) with the measured messages.
- [x] `dotnet build Curl.Networking.UnitLibrary -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes and no new test needs `TestCategory=Integration`; `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Networking`.

## Notes

- Plan item: N3 in the Phase 1 HTTP plan (2026-09-26); plan keys in this file were replaced by their task IDs.
- Delivered: `SocksProxyTunnel` (dispatch, exact reads), `Socks4Handshake` (SOCKS4/4a), `Socks5Handshake` (SOCKS5/5h, RFC 1929 auth), wired into `TcpConnector.ConnectThroughProxyAsync`; HTTPS proxies still throw `NotSupportedException`. Tests: `TcpConnectorTests.Socks.cs`, all through `TcpConnector` with `ScriptedConnection`.
- Plan and review were done in-session rather than through protocol-architect: the change is one library and its tests, following the HTTP CONNECT tunnel's shape (ADR-0023). code-reviewer found no correctness defects.

### Measurement (2026-09-27)

A Python loopback server scripted per case (read N bytes / send hex / close / answer one HTTP request) ran `C:/Program Files/Git/mingw64/bin/curl.exe` (curl 8.21.0 x86_64-w64-mingw32, Schannel) with `-s -S -x <proxy> <url>`; the proxy was `127.0.0.1:19080`, the target port 8080 (`1f 90`). Bytes as received / sent:

| Command | curl sent | Proxy answered | Result |
| --- | --- | --- | --- |
| `-x socks4://… http://127.0.0.1:8080/` | `04 01 1f 90 7f 00 00 01 00` | `00 5a 00×6` | 0, HTTP request follows |
| `-x socks4://… http://localhost:8080/` | `04 01 1f 90 7f 00 00 01 00` | granted | 0 |
| `-x socks4://bob:pw@…` | `04 01 1f 90 7f 00 00 01 62 6f 62 00` (no password) | granted | 0 |
| `-x socks4://…`, reply `00 5b 12 34 c0 a8 01 02` | | | 97 `[SOCKS] cannot complete SOCKS4 connection to 192.168.1.2:4660. (91), request rejected or failed.` |
| reply code `5c` / `5d` / `5e` | | | 97 `… (92), request rejected because SOCKS server cannot connect to identd on the client.` / `(93), request rejected because the client program and identd report different user-ids.` / `(94), Unknown.` |
| reply `01 5a …` | | | 97 `SOCKS4 reply has wrong version, version should be 0.` |
| reply `00 5a 00` then close, or close | | | 97 `Failed to receive SOCKS response, proxy closed connection` |
| `socks4://` to `http://[::1]:8080/` | nothing | | 97 `SOCKS4 connection to ::1 not supported` |
| `socks4://` to `http://nosuch.invalid:8080/` | nothing (proxy dialled first) | | 6 `Could not resolve host: nosuch.invalid` |
| `socks4://<256×a>@…` (255 works) | nothing | | 97 `Too long SOCKS proxy username` |
| `-x socks4a://… http://example.test:8080/` | `04 01 1f 90 00 00 00 01 00` + `example.test` + `00` | granted | 0 |
| `socks4a://` to `127.0.0.1` / `[::1]` | the literal as a name (`… 00 31 32 37 2e 30 2e 30 2e 31 00`, `… 00 3a 3a 31 00`) | | |
| `socks4a://bob:pw@… example.test` | `04 01 1f 90 00 00 00 01 62 6f 62 00` + `example.test 00` | | 0 |
| `socks4a://` host of 255+ bytes (254 works) | nothing | | 97 `SOCKS4: too long hostname` |
| `-x socks5://… http://127.0.0.1:8080/` | `05 02 00 01`, then `05 01 00 01 7f 00 00 01 1f 90` | `05 00`, `05 00 00 01 7f 00 00 01 1f 90` | 0 |
| `-x socks5h://… http://example.test:8080/` | `05 02 00 01`, `05 01 00 03 0c` + `example.test` + `1f 90` | | 0 |
| `socks5h://` to `127.0.0.1` | `05 01 00 01 7f 00 00 01 1f 90` | | 0 |
| `socks5://` to `[::1]` | `05 01 00 04 00×15 01 1f 90` | | 0 |
| `socks5://bob:pw@…` | `05 03 00 01 02`, `01 03 62 6f 62 02 70 77` | `05 02`, `01 00` | 0 |
| same, proxy picks `00` | `05 03 00 01 02`, then CONNECT | | 0 |
| no credential, proxy picks `02` | `05 02 00 01`, `01 00 00` | | 0 with `01 00` |
| auth answered `01 01` | | | 97 `User was rejected by the SOCKS5 server (1 1).` |
| method `ff` / `03` / version `04` | | | 97 `No authentication method was acceptable.` / `Unknown SOCKS5 mode attempted to be used by server.` / `Received invalid version in initial SOCKS5 response.` |
| method `01` (GSSAPI) | | | 97 `SSPI error: InitializeSecurityContext failed: SEC_E_TARGET_UNKNOWN (0x80090303) - The specified target is unknown or unreachable` |
| CONNECT reply status 1–9, 255 | | `05 0N 00 01 12 34 56 78 04 d2` | 97 `cannot complete SOCKS5 connection to 127.0.0.1. (N)`; names the URL host (`example.test`, `localhost`, `::1`) |
| reply version `04` / address type `09` | | | 97 `SOCKS5 reply has wrong version, version should be 5.` / `SOCKS5 reply has wrong address type.` |
| reply bound to IPv6 (22 bytes) or name (`03 03 abc 00 50`) | | | 0 |
| reply `05 00 00 01` then close | | | 97 `Failed to receive SOCKS response, proxy closed connection` |
| `socks5h://` host of 256 bytes (255 works) | nothing | | 97 `SOCKS5: the destination hostname is too long to be resolved remotely by the proxy.` |
| `socks5://<256×a>:pw@…` / password of 256 | `05 03 00 01 02` only | `05 02` | 97 `Excessive username length for proxy auth` / `Excessive password length for proxy auth` |
| `socks5://` to `nosuch.invalid` | `05 02 00 01` | `05 00` | 6 `Could not resolve host: nosuch.invalid` |
| `-x socks5://127.0.0.1:1` | | | 7 `Failed to connect to 127.0.0.1:8080 over proxy 127.0.0.1 after 2040 ms: Could not connect to server` |
| `-x socks4://nosuch.invalid:1` | | | 5 `Could not resolve proxy: nosuch.invalid` |

### Decisions (defaults taken; the ADR is BL-354)

- The ADR could not be written here: `Documentation/Planning/Decisions` is in BL-302's `touches` (in Doing), so rather than send this task back to Backlog for one document, BL-354 is filed to record the decisions below in an ADR.
- GSSAPI is offered in the SOCKS5 greeting because the reference build offers it (so the bytes match), but is not implemented; a proxy that picks it fails with the SSPI message measured above.
- SOCKS4 sends the first IPv4 address resolved; with none, exit 97 naming the first address. SOCKS5 sends the first address, either family. A host is an address literal only if `IPAddress.TryParse` accepts it and, for IPv4, it prints back the same (so `1` is a name). Names and credentials are UTF-8.
- An exception while the handshake is written or read disposes the proxy connection and propagates, as the HTTP CONNECT path does.

### Quality

- `dotnet build -warnaserror` clean; `dotnet format --verify-no-changes` clean for both projects; `dotnet test --filter "TestCategory!=Integration"` green in all 16 test projects.
- `Measure-CodeQuality.ps1 -Library Curl.Networking.UnitLibrary`: every new and changed member at 100% line and branch, complexity at most 10. The four members it still lists were failing before this task and are untouched: `TcpDialer.DialAsync`, `UdpDatagramChannel.SendAsync`/`ReceiveAsync` (covered only by `Integration` tests) and `SslStreamTlsProvider.CreateCipherSuitesPolicy` line 311 (reachable only on Linux).
- Found and fixed on the way: `AuthenticateAsClientAsync_WhenTheServerSendsAnIntermediate_ReportsItAfterTheServersCertificate` started failing (`X509Chain.Build`: unknown chain building error) because on Windows `SslStreamCertificateContext.Create` saves the intermediate into CurrentUser\CA, and 101 `CN=BL303 Intermediate` certificates had built up. The test now removes its intermediate after the handshake (removing it before stops Schannel sending it), and the 101 leftovers were deleted from the store. Other lanes running the old test can still add one each run until this lands.

## Log

- 2026-09-26: Created.
- 2026-09-27: Backlog -> Doing.
- 2026-09-27: Doing -> Done. TcpConnector tunnels through SOCKS4, SOCKS4a, SOCKS5 and SOCKS5h proxies with curl 8.21.0's measured bytes and exit 97 messages
