---
id: BL-644
title: Parse --happy-eyeballs-timeout-ms and race address families after that delay
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Cli.UnitLibrary, Curl.Cli.UnitTests, Curl.Networking.UnitLibrary, Curl.Networking.UnitTests, Documentation/Planning/Decisions/ADR-0254-tcpconnector-races-address-families-after-the-happy-eyeballs-timeout.md, Tasks/Backlog/BL-889-pass-happy-eyeballs-timeout-ms-into-httprequestoptions-happy.md]
requirement: none
created: 2026-09-28
completed: 2026-09-29
---
# BL-644 — Parse --happy-eyeballs-timeout-ms and race address families after that delay

## Goal

`--happy-eyeballs-timeout-ms <ms>` parses (default 200) and the TCP connector starts the second address family's attempt that long after the first, as curl 8.21.0 does, where today it tries addresses in turn.

## Context

- Conformance audit 2026-09-28, row 28 (Minor).
- Code: `Curl.Networking.UnitLibrary/TcpConnector.cs` (dial order), with `TimeProvider` for the delay; `-4`/`-6` (BL-500) restrict the families. Measure what `-v` prints for the racing attempts and which address `%{remote_ip}` reports when both succeed.

## Acceptance criteria

- [x] Measured first with `Record-CurlExchange.ps1` listening on one family only (`-ListenAddress`), against `localhost` with `-v` and `--happy-eyeballs-timeout-ms 50` and `5000`; stderr and timing copied into Notes.
- [x] `Curl.Networking.UnitTests` on a fake `TimeProvider` pin when each family's dial starts and which connection wins; `Curl.Cli.UnitTests` pin parsing and refusals.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for each library changed.

## Notes

### Measured (curl 8.21.0 mingw Schannel, 2026-09-29)

`Record-CurlExchange.ps1 -ListenAddress <one family> -Port 18644 -CurlArgs -v -s -o NUL -w '%{remote_ip} %{time_connect}\n' --happy-eyeballs-timeout-ms <ms> http://localhost:18644/`:

- 50 ms, IPv4 listening (curl ran 148 ms):
  ```
  * Host localhost:18644 was resolved.
  * IPv6: ::1
  * IPv4: 127.0.0.1
  *   Trying [::1]:18644...
  *   Trying 127.0.0.1:18644...
  * Established connection to localhost (127.0.0.1 port 18644) from 127.0.0.1 port 59108
  ```
  stdout `127.0.0.1 0.055197`. The abandoned `::1` attempt prints nothing.
- 5000 ms, IPv4 listening (curl ran 2063 ms):
  ```
  *   Trying [::1]:18644...
  * connect to ::1 port 18644 from :: port 59110 failed: Connection refused
  *   Trying 127.0.0.1:18644...
  * Established connection to localhost (127.0.0.1 port 18644) from 127.0.0.1 port 59111
  ```
  stdout `127.0.0.1 2.021883`: Windows retries the SYN to the closed `::1` port for about 2 s, and IPv4 starts the moment IPv6 fails.
- 50 and 5000 ms, IPv6 listening: only `Trying [::1]:18644...`, then established; stdout `::1 0.000671` / `::1 0.000682`.
- 0 ms, IPv4 listening: both `Trying` lines at once, `127.0.0.1 0.029620`. No option: both lines, `127.0.0.1 0.288050` (the 200 ms default).
- Parsing: `0`, `007`, `2147483647` accepted; `-1` "expected a positive numerical parameter"; `abc`, `1.5`, `+5`, `0x10`, `1e3`, ` 7`, empty, `2147483648`, `9223372036854775807` "expected a proper numerical parameter"; no value "requires parameter"; `--no-happy-eyeballs-timeout-ms` "the given option cannot be reversed with a --no- prefix". All exit 2.

### Done

- `Curl.Networking.UnitLibrary/AddressFamilyRace.cs` races the families (ADR-0254); `TcpConnector` takes `happyEyeballsTimeout` (null for 200 ms, held to 0..~49.7 days) and dials direct and proxy connects through it. Tests: `TcpConnectorTests.HappyEyeballs.cs` with the new `Fakes/GatedTcpDialer.cs`.
- `Curl.Cli.UnitLibrary`: `--happy-eyeballs-timeout-ms` row (per option group), `CommandLineOptions.HappyEyeballsTimeout`, `CommandLineNumber.ParseMilliseconds`. Tests: `CommandLineHappyEyeballsTimeoutTests.cs`. `--ai-help` now lists the option as supported by itself (its "Not supported" line comes from the option table), so no help text changed.
- Measure-CodeQuality: Curl.Networking.UnitLibrary 100/100, 0 failing (worst CRAP 10); Curl.Cli.UnitLibrary 100/100, 0 failing (worst CRAP 10). Fast tests: all 33 test projects pass.

### Choices

- Wiring into `Curl.Console` (`CurlComposition.CreateTcpConnector`) is outside this task's `touches` and BL-773 holds `Curl.Console`; BL-889 already depends on BL-644 and owns passing this option into `HttpRequestOptions`, so its goal and criteria now also cover the connector. Until it lands the connector races with curl's 200 ms default.
- Within one family, addresses are tried in turn after each failure (as before); curl may start the next sooner. Recorded as a consequence in ADR-0254.
- ADR-0254 is a new file in `Documentation/Planning/Decisions`, which BL-887 (Doing) also names; BL-887 renumbers existing ADRs and edits no file this task adds, so the new file cannot conflict. The ADR `README.md` index is left for BL-887/align-and-document so this task edits no file BL-887 does. Added the ADR file's path to `touches`.
- `dotnet format whitespace` rewrote line endings in seven unrelated files; they are left uncommitted.

## Log

- 2026-09-28: Created.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Done. --happy-eyeballs-timeout-ms parses (default 200) and TcpConnector races the second address family after that delay or once the first fails
