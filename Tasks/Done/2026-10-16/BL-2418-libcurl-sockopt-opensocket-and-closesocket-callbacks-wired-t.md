---
id: BL-2418
title: Libcurl: sockopt, opensocket and closesocket callbacks wired through the engine
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2417]
touches: [Libcurl/Callbacks/Sockets, Libcurl/CLAUDE.md, Libcurl.UnitTests/Callbacks/Sockets, Curl.Engine.UnitLibrary, Curl.Engine.UnitTests]
requirement: none
created: 2026-10-15
completed: 2026-10-16
---
# BL-2418 — Libcurl: sockopt, opensocket and closesocket callbacks wired through the engine

## Goal

CURLOPT_SOCKOPTFUNCTION, OPENSOCKETFUNCTION and CLOSESOCKETFUNCTION are called by the engine around each connection; sockets are opaque `CurlSocket` handles over `IConnection`.

## Context

ADR-0506; phase 3 of `Documentation/Planning/Curl-Api-And-libcurl.md`. Read the seam as it is in `Curl.Api.UnitLibrary` (EngineContracts, Options, Callbacks, Info, Codes) and the managed engine as it is in `Curl.Engine.UnitLibrary` now, not the plan's guesses, and correct the plan where they differ. Libcurl code and tests live in the area sub-folders named in `touches` (`Libcurl/<Area>` with its tests in `Libcurl.UnitTests/<Area>`) so areas run in parallel lanes; Libcurl tests use fakes of the seam interfaces, never the network. `--ai-help` is untouched (no option changes).

## Acceptance criteria

- [x] Engine tests pin the call order and the refusal outcomes.
- [x] `dotnet build` is clean and the fast tests pass with no test deleted, skipped or weakened.
- [x] `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` is unchanged or grown, and every case it lists still passes.
- [x] `Measure-CodeQuality.ps1 -Library Libcurl,Curl.Engine.UnitLibrary` reports 0 failing members (100% line and branch coverage, complexity at most 10, CRAP at most 30).
- [x] Every new public member has an XML doc comment, and `Libcurl/CLAUDE.md` or `Curl.Engine.UnitLibrary`'s CLAUDE.md is kept true.

## Notes

- Design (ADR-0610): `Curl.Engine`'s `SocketCallbackConnector` sits beneath the pool (`ManagedCurlEngine.PoolOf`, `PoolingConnector.Over`) and under connect-only, only when a socket callback is set. The engine's sockets live in `Curl.Networking` (outside `touches`) and are never handed out, so the callbacks run after the inner connect, over opaque `CurlSocket` handles (the open-socket callback's, else `CurlSocketHandles` numbers from 1). `CURL_SOCKET_BAD` -> exit 7, `CURL_SOCKOPT_ERROR` -> close-socket then exit 42, each `Failed to connect to <host>:<port> after <n> ms: <strerror>` (curl 8.22.0 `cf_socket_open` + happy-eyeballs failf; not measurable through the curl tool, which exposes no such callback, so taken from upstream source). `ALREADY_CONNECTED` carries on as OK. Close-socket runs once when the connection really closes.
- The plan's seam held as written (`EasyCallbackOptions.SocketOption`, `OpenSocket`, `CloseSocket`); no correction needed.
- `Libcurl/CLAUDE.md` gained one sentence (acceptance criterion 5 asks it be kept true), as BL-2417 did; it lies outside `touches` but in the Libcurl project no Doing task edits.
- Follow-up: BL-2474 (QUIC connections pass through without the callbacks).
- Measured: `Measure-CodeQuality.ps1 -Library Libcurl,Curl.Engine.UnitLibrary` 0 failing members; fast tests all green (Engine 1473 passed, Libcurl 955 passed).

## Log

- 2026-10-15: Created.
- 2026-10-16: Backlog -> Doing.
- 2026-10-16: Doing -> Done. CURLOPT_SOCKOPTFUNCTION, OPENSOCKETFUNCTION and CLOSESOCKETFUNCTION run around each engine TCP connection over opaque CurlSocket handles
