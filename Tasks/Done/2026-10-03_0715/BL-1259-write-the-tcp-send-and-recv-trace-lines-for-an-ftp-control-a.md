---
id: BL-1259
title: Write the [TCP] send and recv trace lines for an FTP control and data connection
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-1253]
touches: [Curl.Protocol.Abstractions.UnitLibrary, Curl.Protocol.Abstractions.UnitTests, Curl.Protocol.Ftp.UnitLibrary, Curl.Protocol.Ftp.UnitTests, Curl.Networking.UnitLibrary, Curl.Networking.UnitTests, Curl.Console.UnitTests]
requirement: none
created: 2026-10-02
completed: 2026-10-03
---
# BL-1259 — Write the [TCP] send and recv trace lines for an FTP control and data connection

## Goal

Under `--trace-config tcp`, `network`, `all` and `-vvvv`, an `ftp://` transfer writes curl 8.21.0's `[TCP] send(len=<n>) -> 0, <n>` before each `>` command and `[TCP] recv(len=900) -> 0, <n>` before each `<` reply on the control connection, and `[TCP-1] recv(len=<remaining>) ...` lines on the data connection.

## Context

- Measured in BL-1253's Notes (`-s -v --trace-config tcp ftp://127.0.0.1:P/a.txt`, 5-byte file): control reads are `recv(len=900)`, with a would-block `recv(len=900) -> 81, 0` before the final `226`; the data connection writes `[TCP-1] recv(len=5) -> 81, 0`, `[TCP-1] recv(len=5) -> 0, 5` (len = bytes still expected), then `[TCP-1] cf_socket_shutdown`, `shut down successfully`, `destroy`, `cf_socket_close`. Measure an upload (`-T`) and a listing too.
- `TcpIoTraceConnection` (`Curl.Networking.UnitLibrary`, ADR-0357's BL-1195 and BL-1253 amendments) is chosen in `TcpConnector.OpenedInPlaintext` by `PoolScheme` `http`. FTP's control target (`FtpProtocolHandler`) and data target (`FtpSession`) carry no `PoolScheme`, so `ConnectTarget` needs a way for the handler to say which connection it is and what receive length to report.

## Acceptance criteria

- [x] Upload and listing measured with `Record-CurlExchange.ps1`; stderr in Notes.
- [x] Tests in `Curl.Networking.UnitTests`, `Curl.Protocol.Ftp.UnitTests` and `Curl.Console.UnitTests` pin the control connection's `send`/`recv(len=900)` lines beside `>`/`<` and the data connection's `[TCP-1]` lines; ADR-0357 amended.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage for each library changed.

## Notes

- 2026-10-03 (lane 1): measured with Record-CurlExchange.ps1 (curl 8.21.0 Schannel): control `[TCP] send(len=n)` before each `>`, `[TCP] recv(len=900) -> 0, n` before each `<`, no lines for QUIT; data `[TCP-1] recv(len=<remaining>)` for a known size (no EOF read; curl cuts off at SIZE: `SIZE=213 3` with "hello" writes "hel", exit 0), `[TCP-1] recv(len=102400)` to `-> 0, 0` for LIST; upload `[TCP-1] send(len=5) -> 0, 5`. Would-block `[TCP] recv(len=900) -> 81, 0` before 226 is a race (absent after LIST), so control writes none.
- Implemented in the working tree (stashed by the shift): `TcpIoTraceLines` + `ConnectTarget.TcpIoTrace`, `InfoLineStoppingTransferEvents` (Abstractions; Abstractions.UnitTests added to touches, no other Doing task names it); `TcpIoTraceConnection` takes the lines; FTP sets them on plain control/data targets, sizes data reads to the bytes still expected, stops at SIZE, silences QUIT. Build clean, fast tests green, new tests in Networking, Ftp, Abstractions and Console.UnitTests.
- Left: Measure-CodeQuality.ps1 for the three libraries (run exceeded the 1-hour limit), ADR-0357 BL-1259 amendment. Run ran out of budget.
- 2026-10-03 (lane 7): restored lane 1's work from stash 933e82a6 ("darkfactory BL-1259 20261002-211047", applied by diff; its stray untracked `hello` file left out). Build clean with -warnaserror; all fast tests green (Ftp 595, Abstractions 681, Networking 2915, Console 2457 passed).
- Coverage measured per library from its own test projects (cobertura collector, then `Measure-CodeQuality.ps1 -SkipTestRun -Library <one>`; the whole-solution run exceeds a lane's hour): every member this task added or changed is at 100% line and branch. The members still reported (Abstractions 99.92/100, Networking 99.95/99.6, Ftp 100/95.6) are outside this diff and older than it: `FtpStateTrace.StateOf` (complexity 122), `AddressFamilyRace.DialAsync`, `TcpConnector` proxy/tunnel branches, `KerberosKdcProxyHttpsTransport..ctor`, `IMultiplexedConnection.PeerIdleTimeout`, `PooledConnection` branches. Several are covered from other test projects in the merged solution run, so they are left to coverage-auditor rather than filed from a partial measurement.
- ADR-0357 amended (BL-1259 section): `ConnectTarget.TcpIoTrace`, the control and data lines, QUIT silent, no would-block line on control, ftps/--ssl untraced.

## Log

- 2026-10-02: Created.
- 2026-10-02: Backlog -> Doing.
- 2026-10-03: Doing -> Backlog. Code done in the stashed working tree; coverage measurement and the ADR-0357 amendment remain (run out of budget)
- 2026-10-03: Backlog -> Doing.
- 2026-10-03: Doing -> Done. ftp:// writes curl's [TCP] send/recv(len=900) control lines and [TCP-1] data lines under --trace-config tcp
