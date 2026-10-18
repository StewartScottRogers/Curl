---
id: BL-2623
title: Make CURLOPT_CONNECT_ONLY through a multi handle connect in upstream cases 597 and 660
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2705, BL-2706]
touches: [Curl.Engine.UnitLibrary, Curl.Engine.UnitTests, Curl.Conformance.UnitTests/PassingUpstreamCases.txt]
requirement: none
created: 2026-10-17
completed: 2026-10-18
---
# BL-2623 — Make CURLOPT_CONNECT_ONLY through a multi handle connect in upstream cases 597 and 660

## Goal

Upstream cases 597 (FTP) and 660 (IMAP), both lib597, pass: a `CURLOPT_CONNECT_ONLY` transfer through a multi handle connects and logs in, then completes with a done message.

## Context

Found by BL-2533: `Lib597` is unit tested and matches lib597.c, but nothing reaches the server: 597 `<verify><protocol> differs at byte 0 (line 1): expected "USER anonymous\r\n", got the end`; 660 `expected "A001 CAPABILITY\r\n", got the end`. lib556 shows connect-only works through `curl_easy_perform`; the multi path does not. In the first pass find the Libcurl multi or engine project at fault and add it to `touches` before any edit.

## Acceptance criteria

- [x] Each named case that passes through the runner on Windows is added to `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`; one that still fails is filed with the board script with its differing lines.
- [x] Any product fix has a focused unit test in the fixed library's own `.UnitTests` project (ADR-0482).
- [x] No added case's output depends on the platform.
- [x] `dotnet build` is clean and the fast tests pass with no test deleted, skipped or weakened.

## Notes

- 2026-10-17 (lane 1, first pass, no code edited): the fault is not the multi path. `Lib597` reaches the server through `CurlMulti` -> `ManagedMultiTransfer`, which runs each transfer's own `ManagedEasyTransfer.PerformAsync`, exactly as `curl_easy_perform` does. The cause is that Curl's `CURLOPT_CONNECT_ONLY` 1 is a raw TCP connect for every scheme (`ManagedEasyTransfer.IsRawConnectOnly` -> `PerformConnectOnlyAsync`), while libcurl runs the protocol's connect phase first: FTP logs in (`USER`, `PASS`, `PWD`; case 597) and IMAP sends `A001 CAPABILITY` (case 660). lib556 passes only because HTTP has no connect phase.
- No protocol handler has a connect-only entry point except `WsProtocolHandler.ConnectAndUpgradeOnlyAsync`. Filed BL-2705 (FTP) and BL-2706 (IMAP) for those entry points; this task keeps the engine wiring: in `PerformThroughProxyAsync`, under `ConnectOnly.Connect`, route `ftp`/`ftps` and `imap`/`imaps` to the handler's entry point (as `WebSocketUpgradeDispatcherOf` does for `ws`) and keep the returned connection in `ConnectOnlyConnection`, then add 597 and 660 to the ratchet. Added `Curl.Engine.UnitLibrary` and `Curl.Engine.UnitTests` to `touches` for that wiring (no task in Doing on `origin/work/dark-factory` names them). POP3 and SMTP connect-only are not needed by these cases and are left to a later task if a case needs them.
- 2026-10-18 (lane 1): done. `IsRawConnectOnly` leaves `ftp`/`ftps`/`imap`/`imaps` (`LogsInBeforeConnectOnly`) to the normal perform path, where `FollowerOf` picks `ConnectOnlyLoginDispatcherOf`: `FtpConnectOnlyHandler` and `ImapConnectOnlyHandler` (new, shaped like `WebSocketUpgradeOnlyHandler`) built by `ManagedCurlEngine.CreateConnectOnlyLoginHandlers` over `CreateConnectOnlyConnector`'s connector, keeping the open connection in `ConnectOnlyConnection`. ADR-0681 records it. Wired in `FollowerOf` rather than `PerformThroughProxyAsync` so the context, proxy and netrc lookup are the normal path's.
- Measured: cases 597 and 660 pass through the runner (Manual one-case test) and are on the ratchet; both are protocol bytes over the in-memory servers, with no path, OS error or certificate, so platform-neutral. Tests: 3 new in `ManagedEasyTransferTests.ConnectOnly.cs` (FTP login, refused FTP login = 67, IMAP CAPABILITY). Measure-CodeQuality -Library Curl.Engine.UnitLibrary: 100% line, 100% branch, 0 failing members, worst CRAP 10. `dotnet build` 0 warnings; fast tests green in all 42 test projects.

## Log

- 2026-10-17: Created.
- 2026-10-17: Backlog -> Doing. Criteria at claim: 92cbe6ef 4963d26c 21e5ddeb 4272a6b9.
- 2026-10-17: Doing -> Backlog. Waits on BL-2705 (FTP connect-only login) and BL-2706 (IMAP connect-only CAPABILITY); the engine wiring and ratchet follow
- 2026-10-18: Backlog -> Doing. Criteria at claim: 92cbe6ef 4963d26c 21e5ddeb 4272a6b9.
- 2026-10-18: Doing -> Done. CURLOPT_CONNECT_ONLY 1 on ftp/imap URLs runs the FTP login or IMAP CAPABILITY connect phase; upstream cases 597 and 660 pass and are on the ratchet
