---
id: BL-2313
title: Define IMultiTransfer with the socket-action API, timers and CURLMOPT options in Curl.Api.UnitLibrary
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2312]
touches: [Curl.Api.UnitLibrary/EngineContracts/ICurlEngine.cs, Curl.Api.UnitLibrary/EngineContracts/Multi, Curl.Api.UnitTests/EngineContracts/Multi]
requirement: none
created: 2026-10-15
completed: 2026-10-15
---
# BL-2313 — Define IMultiTransfer with the socket-action API, timers and CURLMOPT options in Curl.Api.UnitLibrary

## Goal

`Curl.Api.UnitLibrary/EngineContracts/Multi` defines `IMultiTransfer` - add and remove easy transfers, perform, wait, poll, wakeup, info-read messages, the socket-action API with socket and timer callbacks, and the `CURLMOPT_*` options - and `ICurlEngine` gains `CreateMulti()`.

## Context

ADR-0506; plan section "Multi"; needs BL-2312. Upstream: curl 8.22.0's `include/curl/multi.h` and `docs/libcurl/curl_multi_*.md`. Contract only; the engine's multi implementation is phase 3.

- `EasyMultiOptions` (in this folder): `MAXCONNECTS`, `MAX_HOST_CONNECTIONS`, `MAX_TOTAL_CONNECTIONS`, `MAX_CONCURRENT_STREAMS`, `PIPELINING` (`CURLPIPE_*` flags), `NETWORK_CHANGED`, the socket callback (`CURLMOPT_SOCKETFUNCTION`: socket, `CURL_POLL_*` action), timer callback (`CURLMOPT_TIMERFUNCTION`: timeout or -1), push callback (`CURLMOPT_PUSHFUNCTION`, `CURL_PUSH_*`).
- `IMultiTransfer` (`IAsyncDisposable`): `Add(IEasyTransfer)`, `Remove(IEasyTransfer)` returning `CurlMultiCode`; `PerformAsync` returning running count; `WaitAsync`/`PollAsync(timeout, extra waits)`; `Wakeup()`; `TryReadInfo` yielding `CURLMSG_DONE` messages (transfer + `CurlCode`); `SocketActionAsync(CurlSocket, CURL_CSELECT_* flags)` with `CURL_SOCKET_TIMEOUT`; `Assign(CurlSocket, object?)`; `Timeout`; `GetHandles()`; `WaitFds`; `GetOffset` for `CURLMINFO_*`; `NotifyEnable/Disable`. `CurlSocket` is the opaque value from `Curl.Api.Callbacks` (BL-2301).
- `curl_multi_fdset` has no .NET meaning; document that `Libcurl` answers it from the socket set (phase 3 decides), and do not add a member for it here.

## Acceptance criteria

- [x] `IMultiTransfer`, `EasyMultiOptions` (named `MultiOptions`, see Notes), the multi message record and the `CURL_POLL_*`, `CURL_CSELECT_*`, `CURLPIPE_*`, `CURL_PUSH_*`, `CURLMINFO_*` values exist in `Curl.Api.UnitLibrary/EngineContracts/Multi`, each member documented with the libcurl name it stands for.
- [x] `ICurlEngine` has `CreateMulti()`.
- [x] Tests in `Curl.Api.UnitTests/EngineContracts/Multi` pin every upstream constant and `EasyMultiOptions`' defaults (libcurl 8.22.0's).
- [x] `dotnet build` is clean (warnings are errors).
- [x] `dotnet test --filter "TestCategory!=Integration"` passes.
- [x] `powershell -NoProfile -File Measure-CodeQuality.ps1` passes (100% line and branch coverage, complexity <= 10, CRAP <= 30).
- [x] `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` is unchanged.

## Notes

- Decisions recorded in ADR-0510. The options record is `MultiOptions`, not `EasyMultiOptions`:
  the `Easy*Options` records are `curl_easy_setopt` groups, so the prefix would be false.
- `curl_multi_get_offt` is `GetInfo(CurlMultiInfo)` and `curl_multi_waitfds` is
  `GetWaitSockets`, not `GetOffset`/`WaitFds`, so the names say what is read.
- Also pinned beyond the list: `CURLMSG_*`, `CURL_WAIT_POLL*`, `CURLMNOTIFY_*`, `CURLMNWC_*`
  (needed by `MultiOptions.NetworkChanged`, the notify API and `struct curl_waitfd`), and
  `CURL_SOCKET_TIMEOUT` as `CurlMultiSockets.Timeout`.
- Measured: `Measure-CodeQuality.ps1 -Library Curl.Api.UnitLibrary`: 100% line, 100% branch,
  0 failing members.

## Log

- 2026-10-15: Created.
- 2026-10-15: Backlog -> Doing.
- 2026-10-15: Doing -> Done. IMultiTransfer, MultiOptions and the multi.h constants are defined and pinned; ICurlEngine has CreateMulti()
