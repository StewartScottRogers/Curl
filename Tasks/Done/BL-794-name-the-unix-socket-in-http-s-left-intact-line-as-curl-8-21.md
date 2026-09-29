---
id: BL-794
title: Name the Unix socket in HTTP's left-intact line as curl 8.21.0 does
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-507]
touches: [Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests, Curl.Protocol.Abstractions.UnitLibrary, Curl.Protocol.Abstractions.UnitTests]
requirement: none
created: 2026-09-28
completed: 2026-09-29
---
# BL-794 — Name the Unix socket in HTTP's left-intact line as curl 8.21.0 does

## Goal

Through `--unix-socket <path>`, `-v` ends an HTTP transfer with curl 8.21.0's `Connection #N to host <path lower-cased>:0 left intact`, where it now prints the URL's host and port.

## Context

- Found while doing BL-507 (ADR-0149, Consequences). Measured 2026-09-28 with curl 8.21.0 (mingw, Schannel) and `Record-CurlExchange.ps1 -UnixSocket C:\Users\Public\s.sock`: `-v --unix-socket C:\Users\Public\s.sock http://example.com:8080/x` printed `* Connection #0 to host c:\users\public\s.sock:0 left intact`; ours prints `example.com:8080`.
- The line is built by `Curl.Protocol.Http.UnitLibrary/HttpConnectionInfoLines.cs` from the target's host and port. The handler learns nothing of the socket today: `ConnectResult` (Abstractions) would need to say the connection went through a Unix socket and name its path, which `TcpConnector` (BL-507) knows.
- Check whether the `Reusing existing http: connection with host <host>` line changes too (measured: it names the URL's host, `LocalHost` as typed) and whether a `-L` hop or `--next` group changes it.

## Acceptance criteria

- [x] Measured first with `Record-CurlExchange.ps1 -UnixSocket` on the reference build (a path with capitals, and an abstract name on Linux if available); lines copied into Notes.
- [x] `Curl.Protocol.Http.UnitTests` pins the left-intact line for a Unix socket connection as measured, and the TCP line unchanged.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for every library touched.

## Notes

- Measured 2026-09-29, curl 8.21.0 (x86_64-w64-mingw32, Schannel), `Record-CurlExchange.ps1 -UnixSocket`:
  - `-v --unix-socket "C:\Users\Public\BL794 Sock.sock" http://Example.COM:8080/x` →
    `*   Trying C:\Users\Public\BL794 Sock.sock:0...`, `* Established connection to C:\Users\Public\BL794 Sock.sock (C:\Users\Public\BL794 Sock.sock port 0) from  port 0 `, ..., `* Connection #0 to host c:\users\public\bl794 sock.sock:0 left intact`.
  - `-v --unix-socket C:\Users\Public\BL794-A-Rather-Long-Socket-Name-Over-Fortyfive.sock http://Example.COM:8080/x` →
    `*   Trying C:\Users\Public\BL794-A-Rather-Long-Socket-Na:0...` (cut to 45), but
    `* Connection #0 to host c:\users\public\bl794-a-rather-long-socket-name-over-fortyfive.sock:0 left intact` (whole path).
  - No abstract name: no Linux curl on this machine. The name is passed as given.
  - Not re-measured: two URLs over one socket (the recorder serves one request, so a second URL hangs it). The task's Context already records that the `Reusing existing http: connection with host` line names the URL's host, so it is left as it is.
- Decisions (Claude, under Stewart's delegation; small enough for Notes rather than an ADR):
  - `ConnectResult` gains `UnixSocketPath` (a new optional `unixSocketPath` parameter on `Connected`), `null` for TCP and for a failure. The handler prints `LeftIntactOverUnixSocket`: the whole path, ASCII letters lower-cased as curl's `Curl_strntolower` does, port 0. Only ASCII is lowered because curl's lowering is ASCII-only.
  - Split: setting the path in `TcpConnector` and `PoolingConnector` needs `Curl.Networking.UnitLibrary`, which BL-609 (in Doing) holds. The contract and the handler landed here, where every acceptance criterion is met; BL-884 (depends on BL-794) makes the connector set the path, so the end-to-end Goal completes there.
- Fast tests: one full run reported a failing project and a second hung in `Curl.Quic.UnitTests` (neither touched here); a rerun with `--blame-hang-timeout 5m` passed all 32 test projects. Http 1296, Abstractions 605 tests.
- `Measure-CodeQuality.ps1`: Curl.Protocol.Abstractions.UnitLibrary 100/100, 176 members, 0 failing; Curl.Protocol.Http.UnitLibrary 100/100, 628 members, 0 failing.

## Log

- 2026-09-28: Created.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Done. HTTP's -v left-intact line names a Unix socket connection by its lower-cased path and port 0 once the connector sets ConnectResult.UnixSocketPath (BL-884)
