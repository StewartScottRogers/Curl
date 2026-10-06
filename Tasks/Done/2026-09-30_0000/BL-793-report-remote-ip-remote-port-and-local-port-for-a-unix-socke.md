---
id: BL-793
title: Report remote_ip, remote_port and local_port for a Unix socket connection as curl 8.21.0 does
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-507]
touches: [Curl.Console, Curl.Console.UnitTests, Curl.Output.UnitLibrary, Curl.Output.UnitTests, Curl.Protocol.Abstractions.UnitLibrary, Curl.Protocol.Abstractions.UnitTests]
requirement: none
created: 2026-09-28
completed: 2026-09-30
---
# BL-793 — Report remote_ip, remote_port and local_port for a Unix socket connection as curl 8.21.0 does

## Goal

Through `--unix-socket <path>`, `-w '%{remote_ip}|%{remote_port}|%{local_ip}|%{local_port}'` prints curl 8.21.0's `<path cut to 45 characters>|-1||-1`, where it now prints the TCP defaults.

## Context

- Found while doing BL-507 (ADR-0149, Consequences). Measured 2026-09-28 with curl 8.21.0 (mingw, Schannel) and `Record-CurlExchange.ps1 -UnixSocket`: a successful transfer through `C:\Users\Stewart Rogers\AppData\Local\Temp\bl507.sock` printed `[C:\Users\Stewart Rogers\AppData\Local\Temp\bl|-1||-1|1]` for `[%{remote_ip}|%{remote_port}|%{local_ip}|%{local_port}|%{num_connects}]`; a refused one printed `[|-1||-1|0]`.
- The end points come from `Curl.Console/ConnectionEndPointRecorder.cs` into `TransferReport.RemoteEndPoint`/`LocalEndPoint` (Abstractions, `IPEndPoint`), formatted by `Curl.Output.UnitLibrary/TransferWriteOutVariables.cs`. A Unix socket connection's `IConnection.RemoteEndPoint` is a `UnixDomainSocketEndPoint` and its `ConnectResult.LocalEndPoint` is null; `UnixSocketAddress.RemoteIpText` (Networking) is the text curl shows.
- HTTP keeps its own end points (ADR-0119); check what it reports through a socket.

## Acceptance criteria

- [x] Measured first with `Record-CurlExchange.ps1 -UnixSocket` on the reference build: the four variables after a success and after a refused connect, also for `https` if a TLS server over a socket can be arranged; copied into Notes.
- [x] Tests in `Curl.Console.UnitTests` and `Curl.Output.UnitTests` pin the measured values for a Unix socket transfer, and TCP's unchanged.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for every library touched.

## Notes

- Measured 2026-09-30, curl 8.21.0 (mingw, Schannel), `Record-CurlExchange.ps1 -UnixSocket`, `-w '[%{remote_ip}|%{remote_port}|%{local_ip}|%{local_port}|%{num_connects}]'`:
  - success through `C:\Users\Stewart Rogers\AppData\Local\Temp\bl793.sock` (53 chars): `hi[C:\Users\Stewart Rogers\AppData\Local\Temp\bl|-1||-1|1]`
  - success through a 91-character path in the same folder: the same 45-character cut.
  - success through `Z:\b793.sock` (short): `hi[Z:\b793.sock|-1||-1|1]`
  - refused (no socket), `http://x/` and `https://x/` alike: `[|-1||-1|0]`, exit 7.
  - A TLS server over a socket could not be arranged: `Record-CurlExchange.ps1` refuses `-Tls` with `-UnixSocket`. The TLS connections (`SslStreamConnection`, `HandBuiltTlsConnection`) pass the plaintext connection's `RemoteEndPoint` through, so https through a socket reports the same values.
- Before: HTTP left both end points null (its `RemoteEndPoint as IPEndPoint` is null for a socket) and the recorder recorded nothing, so `remote_ip` was empty; the ports already matched.
- Design (my choice, the simplest that stays drop-in): `TransferReport.UnixSocketRemoteIp` (string) carries the `%{remote_ip}` text, rather than widening `RemoteEndPoint` to `EndPoint`, which would ripple into every handler and test that reads `.Address`/`.Port`. `ConnectionEndPointRecorder.Record` now takes any `EndPoint`: an IP end point as before, a `UnixDomainSocketEndPoint` as `UnixSocketAddress.RemoteIpText` (abstract detected from the serialized address's first path byte being NUL, since `ToString()` shows an abstract name as `@name`), anything else records nothing. `%{remote_ip}` prints `UnixSocketRemoteIp` when set; the ports stay -1 because `RemoteEndPoint` stays null. No ADR: this follows ADR-0119's recorder and ADR-0149's measured text.
- Gates: `dotnet build Curl.slnx -warnaserror` clean; fast tests 33/33 projects green; `Measure-CodeQuality.ps1 -Library Curl.Console,Curl.Output.UnitLibrary,Curl.Protocol.Abstractions.UnitLibrary`: 100% line and branch each, 0 failing members.

## Log

- 2026-09-28: Created.
- 2026-09-30: Backlog -> Doing.
- 2026-09-30: Doing -> Done. -w remote_ip/remote_port/local_ip/local_port through --unix-socket print curl 8.21.0's '<path cut to 45>|-1||-1'
