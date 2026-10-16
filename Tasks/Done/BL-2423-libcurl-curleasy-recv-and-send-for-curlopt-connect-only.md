---
id: BL-2423
title: Libcurl: CurlEasy.Recv and Send for CURLOPT_CONNECT_ONLY
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2408, BL-2402]
touches: [Libcurl/Easy, Libcurl.UnitTests/Easy, Curl.Engine.UnitLibrary, Curl.Engine.UnitTests]
requirement: none
created: 2026-10-15
completed: 2026-10-16
---
# BL-2423 — Libcurl: CurlEasy.Recv and Send for CURLOPT_CONNECT_ONLY

## Goal

`CurlEasy.Send` and `Recv` (`curl_easy_send`/`curl_easy_recv`) work after a `CONNECT_ONLY` perform, `CurlCode.Again` when nothing is ready, over `IConnectOnlyConnection`, implemented by the managed engine.

## Context

ADR-0506; phase 3 of `Documentation/Planning/Curl-Api-And-libcurl.md`. Read the seam as it is in `Curl.Api.UnitLibrary` (EngineContracts, Options, Callbacks, Info, Codes) and the managed engine as it is in `Curl.Engine.UnitLibrary` now, not the plan's guesses, and correct the plan where they differ. Libcurl code and tests live in the area sub-folders named in `touches` (`Libcurl/<Area>` with its tests in `Libcurl.UnitTests/<Area>`) so areas run in parallel lanes; Libcurl tests use fakes of the seam interfaces, never the network. `--ai-help` is untouched (no option changes).

## Acceptance criteria

- [x] Engine tests show a connect-only perform, a send and a recv over a fake connection; Libcurl tests show `Again` and the wrong-state result.
- [x] `dotnet build` is clean and the fast tests pass with no test deleted, skipped or weakened.
- [x] `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` is unchanged or grown, and every case it lists still passes.
- [x] `Measure-CodeQuality.ps1 -Library Libcurl,Curl.Engine.UnitLibrary` reports 0 failing members (100% line and branch coverage, complexity at most 10, CRAP at most 30).
- [x] Every new public member has an XML doc comment, and `Libcurl/CLAUDE.md` or `Curl.Engine.UnitLibrary`'s CLAUDE.md is kept true.

## Notes

- Plan (ADR-0608): a `ConnectOnly.Connect` perform connects through `ManagedCurlEngine.CreateConnectOnlyConnector` (the transports' `TcpConnector`, not the pool), sends nothing, and keeps the `IConnection` in the new `ConnectOnlyConnection`. Recv/send never block: a receive starts one read and answers `Again` until it completes; a send hands bytes to one write-and-flush and answers `Again` while it runs. No connection, or options no longer connect-only, answer `UnsupportedProtocol` (libcurl's `easy_connection`).
- Seam as found: `IConnectOnlyConnection` and `CurlConnectOnlyResult` already existed (ADR-0512); the engine's `Connection` threw. `Documentation/Planning/Curl-Api-And-libcurl.md` is outside `touches`, so it is not edited here; ADR-0608 records the shape.
- Libcurl: `CurlEasy.Recv`/`Send` (libcurl's `*n` out parameter, 0 unless `Ok`) block on `RecvAsync`/`SendAsync`.
- `WebSocketAndConnection_NotYetBuilt_ThrowNotSupported` became `WebSocket_NotYetBuilt_ThrowsNotSupported`: `Connection` no longer throws by design.
- Filed BL-2472 (connect-only through a proxy); `ConnectOnly.WebSocket` stays with BL-2443.
- Measured: Measure-CodeQuality -Library Libcurl,Curl.Engine.UnitLibrary: 0 failing members (100/100). Fast tests all green; `PassingUpstreamCases.txt` unchanged.

## Log

- 2026-10-15: Created.
- 2026-10-16: Backlog -> Doing.
- 2026-10-16: Doing -> Done. CurlEasy.Recv and Send work after a CONNECT_ONLY perform of the managed engine, answering Again when nothing is ready
