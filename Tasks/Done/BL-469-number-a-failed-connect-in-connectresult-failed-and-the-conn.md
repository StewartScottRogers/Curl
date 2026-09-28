---
id: BL-469
title: Number a failed connect in ConnectResult.Failed and the connectors
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-453]
touches: [Curl.Protocol.Abstractions.UnitLibrary, Curl.Protocol.Abstractions.UnitTests, Curl.Networking.UnitLibrary, Curl.Networking.UnitTests, Documentation/Planning/Decisions]
requirement: none
created: 2026-09-27
completed: 2026-09-27
---
# BL-469 — Number a failed connect in ConnectResult.Failed and the connectors

## Goal

A failed connect carries curl's number for the connection it tried, so `-v` prints `closing connection #N` with the next number after earlier connections in the same run, as curl 8.21.0 does.

## Context

- ADR-0105 (BL-453): `HttpProtocolHandler` reports `closing connection #N` after a failed connect with `N` from `ConnectResult.ConnectionNumber`, which is `0` for every failure because `ConnectResult.Failed` takes no number.
- curl numbers every connection it creates, failed ones included: a redirect to a refused port after connection `#0` closes `#1`.
- `TcpConnector` (`_nextConnectionNumber`) and `PoolingConnector` number only connections that open.

## Acceptance criteria

- [x] `ConnectResult.Failed` (and `Refused`) accept a connection number; a test in `Curl.Protocol.Abstractions.UnitTests` shows it on `ConnectionNumber`.
- [x] A test in `Curl.Networking.UnitTests` shows `TcpConnector` giving a refused connect after one successful connect number `1`.
- [x] `dotnet build -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes; `Measure-CodeQuality.ps1` reports 100% line and branch coverage for both libraries.

## Notes

- Filed from BL-453 (2026-09-27).
- Measured 2026-09-27, curl 8.21.0 Schannel, `curl -sv http://nohost.invalid/ http://127.0.0.1:1/`:
  the resolve failure closes `#0`, the refused connect after it `#1`. So every failure after
  option parsing takes a number, resolve failures included; a `--resolve`/`--connect-to`
  parse error (exit 49) takes none. Recorded in ADR-0109.
- `Failed` and `Refused` (timings overloads) gained an optional `connectionNumber`. Both
  connectors rebuild a failure with the next number through the internal
  `NumberedConnectFailure.Of`; `PoolingConnector` numbers failures from its own counter, as it
  does successes. A failure is numbered when it completes (choice: avoids threading the number
  through every path to `Opened`; only `--parallel` could show the difference).
- Added `Documentation/Planning/Decisions` to `touches` for ADR-0109; no task in Doing names it.
- Three existing tests asserted the connector returned the provider's failure instance
  (`AreSame`); they now compare exit code and message.
- Gates: build `-warnaserror` clean, fast tests green (Networking 786, Abstractions 538),
  `Measure-CodeQuality.ps1`: both libraries 100% line and branch, 0 failing members.
- `HttpProtocolHandler` already reads `ConnectResult.ConnectionNumber` for the closing line
  (ADR-0105), so no handler change is needed.

## Log

- 2026-09-27: Created.
- 2026-09-27: Backlog -> Doing.
- 2026-09-27: Doing -> Done. A failed connect carries the next connection number from TcpConnector and PoolingConnector, so -v closes #N as curl 8.21.0 does
