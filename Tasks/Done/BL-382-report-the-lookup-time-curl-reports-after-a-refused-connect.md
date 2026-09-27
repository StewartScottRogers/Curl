---
id: BL-382
title: Report the lookup time curl reports after a refused connect
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Abstractions.UnitLibrary, Curl.Protocol.Abstractions.UnitTests, Curl.Networking.UnitLibrary, Curl.Networking.UnitTests, Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests, Documentation/Planning/Decisions]
requirement: none
created: 2026-09-27
completed: 2026-09-27
---
# BL-382 — Report the lookup time curl reports after a refused connect

## Goal

`%{time_namelookup}` after a refused connect prints the lookup time, as curl 8.21.0 prints it, and `ConnectTimings.NameResolved`'s doc comment says what `TcpConnector` does.

## Context

- Found by BL-287 (ADR-0075, "Consequences"). Measured on curl 8.21.0 (mingw, Schannel), 2026-09-27: `curl -s -o NUL -w "ns=%{time_namelookup}|c=%{time_connect}" http://127.0.0.1:1/` printed `ns=0.000048|c=0.000000`, exit 7; for `http://nonexistent.invalid/` (exit 6) `ns=0.000000`. Ours prints `ns=0.000000` for both.
- `ConnectResult.Failed` carries no timings, so `TcpConnector` cannot report the `NameResolved` it took before the dial failed, and `HttpProtocolHandler`'s failed-connect report (`FailedConnectReport`) has no `Connect`. Give a failed result the connector's timings (e.g. a `Failed` overload or `with`-style init), have `TcpConnector` pass `Started` and `NameResolved`, and have the handler copy them. `time_connect` must stay `0.000000` (measured), and `ConnectTimings.Connected` is not nullable today, so decide how a failed connect leaves it unset.
- `ConnectTimings.NameResolved` still documents `null` for a literal address, but `TcpConnector` always sets it (ADR-0030).

## Acceptance criteria

- [x] A `TcpConnectorTests` test pins a refused dial's result carrying `Started` and `NameResolved`, and an `HttpProtocolHandlerTests` test pins a failed connect's report carrying them, with `time_connect` still `0`.
- [x] `ConnectTimings.NameResolved`'s doc comment no longer says it is `null` for a literal address.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Decision (ADR-0091): `ConnectTimings.Connected` is now `long?`, `null` when the connect failed, so `%{time_connect}` prints `0.000000`; a `0` timestamp would have printed `0.000001`. New overloads `ConnectResult.Failed(code, message, timings)` and `ConnectResult.Refused(message, timings)`; the old ones pass `null`.
- `TcpConnector` passes `Started` and `NameResolved` on every dial that reaches no address, direct or to a proxy (the proxy case is the same code path; not measured separately). A failed resolve still carries none, matching curl's measured `ns=0.000000` for exit 6.
- `HttpProtocolHandler.FailedConnectReport` copies `ConnectResult.Timings`. Dict, Gopher, MQTT and Telnet still report no timings on a failed connect; out of this task's `touches`.
- Tests: `TcpConnectorTests.ConnectAsync_WhenTheDialIsRefused_CarriesStartAndLookupWithNoConnect` (plus other-failure, proxy and resolve-failure cases), `HttpProtocolHandlerTests.ExecuteAsync_ConnectRefusedAfterALookup_ReportsTheConnectorsStartAndLookupWithNoConnect`, `ConnectResultTests` for the new overloads, `ConnectTimingsTests.New_ForAFailedDial_LeavesConnectedNull`.
- Checked by hand: the built `curl.exe` prints `ns=0.014827|c=0.000000` exit 7 for `http://127.0.0.1:1/` and `ns=0.000000|c=0.000000` exit 6 for `http://nonexistent.invalid/`.
- Added `Documentation/Planning/Decisions` to `touches` for ADR-0091 and its index row; no task in Doing names it.

## Log

- 2026-09-27: Created.
- 2026-09-27: Backlog -> Doing.
- 2026-09-27: Doing -> Done. %{time_namelookup} after a refused connect prints the lookup time with %{time_connect} still 0, as curl 8.21.0 does
