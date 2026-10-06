---
id: BL-1024
title: Name the forward proxy in the exit 7 and exit 45 connect messages
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests]
requirement: none
created: 2026-09-29
completed: 2026-10-01
---
# BL-1024 — Name the forward proxy in the exit 7 and exit 45 connect messages

## Goal

A connect to a forward HTTP proxy (`-x http://...` with an `http://` URL) that fails names the proxy as curl 8.21.0 does: `Failed to connect to <proxy host>:<proxy port> over proxy <proxy host> after <n> ms: <reason>`.

## Context

- Found in BL-600. Measured on Windows 2026-09-29, curl 8.21.0 Schannel:
  `curl -sS -x http://127.0.0.1:1 http://example.com/` -> exit 7
  `curl: (7) Failed to connect to 127.0.0.1:1 over proxy 127.0.0.1 after 2048 ms: Could not connect to server`;
  Curl prints the same without ` over proxy 127.0.0.1`.
  `curl -sS --interface bogus0 -x http://127.0.0.1:47599 http://example.com/` -> exit 45
  `curl: (45) Failed to connect to 127.0.0.1:47599 over proxy 127.0.0.1 after 2756 ms: Failed binding local connection end`.
- Code: `TcpConnector.ConnectDirectlyAsync` builds the message for a target with `IsForwardProxy`;
  the tunnelling path in `DialAndOpenThroughProxyAsync` already says `over proxy`.

## Acceptance criteria

- [x] Both cases above are measured again with `Record-CurlExchange.ps1` and pinned in `TcpConnectorTests` (exit code and message).
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Networking.UnitLibrary`.

## Notes

- Re-measured 2026-10-01 with `Record-CurlExchange.ps1`, curl 8.21.0 Schannel: exit 7 `Failed to connect to 127.0.0.1:1 over proxy 127.0.0.1 after 2028 ms: Could not connect to server`; exit 45 `Failed to connect to 127.0.0.1:47599 over proxy 127.0.0.1 after 2760 ms: Failed binding local connection end`.
- `TcpConnector.ConnectDirectlyAsync` now appends ` over proxy <target host>` when `IsForwardProxy` is set (after any `--connect-to` ` via` part). Pinned by `ConnectAsync_WhenAForwardProxyFailsToDial_NamesTheProxyInTheCouldntConnectMessage` and `ConnectAsync_WhenAForwardProxyCannotBindTheInterface_NamesTheProxyInTheInterfaceFailedMessage`.
- Gates: build clean with -warnaserror, fast tests green, Measure-CodeQuality reports Curl.Networking.UnitLibrary 100% line and branch, 0 failing members.

## Log

- 2026-09-29: Created.
- 2026-10-01: Backlog -> Doing.
- 2026-10-01: Doing -> Done. A failed connect to a forward HTTP proxy names it: 'over proxy <host>' for exit 7 and exit 45, as curl 8.21.0
