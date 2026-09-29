---
id: BL-645
title: Parse --keepalive-time and --keepalive-cnt and set the TCP keepalive timers
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-490]
touches: [Curl.Cli.UnitLibrary, Curl.Cli.UnitTests, Curl.Networking.UnitLibrary, Curl.Networking.UnitTests]
requirement: none
created: 2026-09-28
completed: 2026-09-29
---
# BL-645 — Parse --keepalive-time and --keepalive-cnt and set the TCP keepalive timers

## Goal

`--keepalive-time <seconds>` (idle and interval, default 60) and `--keepalive-cnt <n>` (probe count) parse with curl 8.21.0's checks and set `TcpKeepAliveTime`, `TcpKeepAliveInterval` and `TcpKeepAliveRetryCount` on the socket where the platform supports them, alongside the SO_KEEPALIVE switch BL-490 wires.

## Context

- Conformance audit 2026-09-28, row 29 (Major, S).
- Socket options go through the `ITcpDialer` seam (ADR-0083); the BCL's `SocketOptionName.TcpKeepAliveTime`/`Interval`/`RetryCount` exist, with platform limits (the retry count is not settable on every Windows version): pin each platform's behaviour.

## Acceptance criteria

- [x] Measured first with `Record-CurlExchange.ps1`: `--keepalive-time 0`, `--keepalive-time -1`, `--keepalive-cnt 0`, `--keepalive-cnt abc`; stderr and exit code copied into Notes.
- [x] `Curl.Cli.UnitTests` pin parsing and refusals; `Curl.Networking.UnitTests` pin the socket options requested through the dialer seam, and none with `--no-keepalive`.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for each library changed.

## Notes

Measured 2026-09-29 against the local curl 8.21.0 (x86_64-w64-mingw32, Schannel) with
`Record-CurlExchange.ps1 -Port 18645` and `http://127.0.0.1:18645/`:

| Arguments | Exit | Standard error |
| --- | --- | --- |
| `--keepalive-time 0` | 0 | progress meter only |
| `--keepalive-time -1` | 2 | `curl: option --keepalive-time: expected a positive numerical parameter` |
| `--keepalive-cnt 0` | 0 | progress meter only |
| `--keepalive-cnt abc` | 2 | `curl: option --keepalive-cnt: expected a proper numerical parameter` |
| `--keepalive-time abc` | 2 | `curl: option --keepalive-time: expected a proper numerical parameter` |
| `--keepalive-cnt -1` | 2 | `curl: option --keepalive-cnt: expected a positive numerical parameter` |
| `--keepalive-time 1.5` | 2 | `... expected a proper numerical parameter` |
| `--keepalive-cnt ""` | 2 | `... expected a proper numerical parameter` |
| `--keepalive-time 2147483647` | 0 | progress meter only |
| `--keepalive-time 2147483648`, `--keepalive-cnt 2147483648` | 2 | `... expected a proper numerical parameter` |
| `--no-keepalive-time 5` | 2 | `curl: option --no-keepalive-time: the given option cannot be reversed with a --no- prefix` |

Every refusal is followed by `curl: try 'curl --help' or 'curl --manual' for more information`.
That is exactly `CommandLineNumber.ParseNonNegative` with the platform `LONG_MAX`, so both options use it.

`--libcurl -`: `--keepalive-time 0 --keepalive-cnt 0` sets only `CURLOPT_TCP_KEEPALIVE 1L` (curl
passes a value only when it is not 0, so libcurl's defaults of 60 seconds and 9 probes stand);
`--keepalive-time 5 --keepalive-cnt 3` adds `CURLOPT_TCP_KEEPIDLE 5L`, `CURLOPT_TCP_KEEPINTVL 5L`
and `CURLOPT_TCP_KEEPCNT 3L`; with `--no-keepalive` none of the four is set.

Choices (matching curl, so no ADR):
- `CommandLineOptions.TcpKeepAliveSeconds` and `TcpKeepAliveProbeCount` hold the value as curl's tool
  does (a `long`, 0 when not given); `TcpSocketOptions.FromCommandLine` maps 0 to libcurl's 60 and 9
  and clamps past `int.MaxValue`, as libcurl's `setopt` does.
- `TcpSocketOptions` now carries `KeepAliveSeconds` (default 60) and `KeepAliveProbeCount` (default 9),
  and `TcpDialer` sets `TcpKeepAliveRetryCount` too, which libcurl always sets. The old
  `TcpSocketOptions.KeepAliveSeconds` constant became `DefaultKeepAliveSeconds`.
- A timer the platform refuses is skipped and the connection goes ahead, as libcurl only logs the
  failure: Linux refuses an idle time or interval past 32767 or more than 127 probes, Windows 11
  refuses more than 255 probes or a negative count (measured with a scratch `dotnet run` app:
  `SocketException` "An invalid argument was supplied."), and clamps the time silently.

Scope: `Curl.Console` builds the dialer (`CurlComposition.cs`) but is held by BL-603 in `Doing`, so the
one-line wiring is filed as BL-869 (depends on this task) rather than widening `touches`.

Tests: `Curl.Cli.UnitTests` 2864 passed (new `CommandLineKeepAliveTimerTests`), `Curl.Networking.UnitTests`
1368 passed (new `TcpSocketOptionsTests`, four new `TcpDialerTests` cases); both libraries 100% line and
branch, 0 failing members.

## Log

- 2026-09-28: Created.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Done. --keepalive-time and --keepalive-cnt parse with curl 8.21.0's checks and TcpDialer sets the keepalive idle time, interval and probe count
