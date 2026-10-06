---
id: BL-511
title: Enforce --max-time and --connect-timeout for dict, gopher, telnet and mqtt
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-498, BL-510]
touches: [Curl.Core.UnitLibrary, Curl.Core.UnitTests, Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests, Curl.Protocol.Tftp.UnitLibrary, Curl.Protocol.Tftp.UnitTests, Curl.Protocol.Dict.UnitLibrary, Curl.Protocol.Dict.UnitTests, Curl.Protocol.Gopher.UnitLibrary, Curl.Protocol.Gopher.UnitTests, Curl.Protocol.Telnet.UnitLibrary, Curl.Protocol.Telnet.UnitTests, Curl.Protocol.Mqtt.UnitLibrary, Curl.Protocol.Mqtt.UnitTests, Curl.Console, Curl.Console.UnitTests, Documentation/Planning/Decisions/ADR-0117-the-connector-owns-the-connect-timeout-and-the-runner-owns-max-time.md]
requirement: none
created: 2026-09-28
completed: 2026-09-28
---
# BL-511 — Enforce --max-time and --connect-timeout for dict, gopher, telnet and mqtt

## Goal

A `dict://`, `gopher://`/`gophers://`, `telnet://` or `mqtt://`/`mqtts://` transfer still running when `-m` runs out ends with exit 28 and `Operation timed out after <ms> milliseconds with <n> bytes received`, as curl 8.21.0 does, where today it never times out.

## Context

- Conformance audit 2026-09-28, row 12 (Blocker). The mechanism is BL-498's ADR (a shared runner deadline, per-handler checks, or both); BL-510 handles the connect phase.
- Handlers: `Curl.Protocol.Dict.UnitLibrary`, `Curl.Protocol.Gopher.UnitLibrary`, `Curl.Protocol.Telnet.UnitLibrary`, `Curl.Protocol.Mqtt.UnitLibrary`; the runner is `Curl.Console/CurlCommandRunner.cs`. If the ADR puts all enforcement in the runner, the protocol projects change only where they must report bytes; leave them untouched otherwise.
- Telnet reading standard input and an MQTT subscription are the long-lived cases; measure both.

## Acceptance criteria

- [x] Measured first with `Record-CurlExchange.ps1` (`-HoldOpenMilliseconds` to keep the server silent): `-m 1` for `dict://`, `gopher://`, `telnet://` and `mqtt://` against a server that sends a few bytes and stalls; stderr and exit code copied into Notes.
- [x] One test per scheme, on a fake `TimeProvider`, pins exit 28 and the measured message with the right byte count.
- [x] A transfer that ends before the deadline is unaffected (existing tests pass unchanged).
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for each library changed.

## Notes

Measured 2026-09-28, curl 8.21.0 (mingw, Schannel), `Record-CurlExchange.ps1 -Response 'hello\r\n' -HoldOpenMilliseconds 6000`.
`-m 3`, not `-m 1`: the recorder's HTTP mode waits up to a second for a request end that dict,
gopher, telnet and mqtt never send before it answers, so `-m 1` would end before any byte came.
The byte count and wording are what the criterion pins; N is `-m`.

- `curl -m 3 dict://127.0.0.1:47811/d:x`: stdout `hello\r\n`, exit 28, `curl: (28) Operation timed out after 3005 milliseconds with 7 bytes received`.
- `curl -m 3 gopher://127.0.0.1:47811/1`: stdout `hello\r\n`, exit 28, `curl: (28) Operation timed out after 3010 milliseconds with 7 bytes received`.
- `curl -m 3 telnet://127.0.0.1:47811/` (standard input closed): stdout `hello\r\n`, exit 28, `curl: (28) Time-out` - telnet.c checks `-m` itself.
- `curl -m 3 mqtt://127.0.0.1:47812/t` against CONNACK `20 02 00 00`, SUBACK `90 03 00 01 00`, PUBLISH `30 05 00 01 't' 'h' 'i'`: stdout `00 01 t h i`, exit 28, `curl: (28) Operation timed out after 3012 milliseconds with 5 out of 5 bytes received`.

Delivered (ADR-0117 Decisions 2 to 4, and its BL-511 amendment, decided by Claude under Stewart's delegation):

- `Curl.Core.MaxTimeWatchdog`, started per attempt by `CurlCommandRunner` beside `LowSpeedWatchdog`; `TransferContextFactory` sets `OperationStarted` from it, wraps the progress sink and links its token. It re-arms a timer that fires before `-m` has passed on the clock.
- dict, gopher, telnet and mqtt report `ReportTransferStarted` after connecting and `ReportDownloaded` after each write (mqtt with the PUBLISH body length as the expected size, as curl's `Curl_pgrsSetDownloadSize`).
- telnet ends a cancelled read with `Time-out` once `-m` has passed on the clock; HTTP's `EndedByLimit` and TFTP's `ReceiveBeforeAsync` treat a cancellation as their own limit once `-m` has passed (Decision 4 tie-break).
- `TransferContextFactory.Create` rose to complexity 16 with the new parameter; its null handling moved into `UploadOf`, `OperationStartedOf`, `WatchedProgress` and a `clock` field.
- `touches` gained ADR-0117 for the amendment; no task in Doing names it.
- Tests: `CurlCommandRunnerMaxTimeTests` (one per scheme end to end, plus before-deadline, connect phase, cancellation, OperationStarted), `MaxTimeWatchdogTests`, `TelnetProtocolHandlerMaxTimeTests`, progress tests in the dict, gopher and mqtt handler tests, tie-break tests in HTTP and TFTP, factory tests.

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
- 2026-09-28: Doing -> Done. -m now ends dict, gopher, telnet and mqtt transfers with exit 28 and curl 8.21.0's message through the runner's MaxTimeWatchdog
