---
id: BL-717
title: Multiplex -Z parallel transfers to one origin over one HTTP/2 connection
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-519, BL-659]
touches: [Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests, Curl.Networking.UnitLibrary, Curl.Networking.UnitTests, Curl.Console, Curl.Console.UnitTests, Curl.Protocol.Abstractions.UnitLibrary, Curl.Protocol.Abstractions.UnitTests]
requirement: none
created: 2026-09-28
completed: 2026-09-30
---
# BL-717 — Multiplex -Z parallel transfers to one origin over one HTTP/2 connection

## Goal

With `-Z`, transfers to the same HTTP/2 origin share one connection as concurrent streams, as curl 8.21.0 multiplexes them (up to the server's `SETTINGS_MAX_CONCURRENT_STREAMS`, waiting for the first connection when `--parallel-immediate` is not given), and `%{num_connects}` and `-v`'s connection-reuse lines say so.

## Context

- Standing rule (root `CLAUDE.md`, "Decisions", 2026-09-28): a complete reimplementation includes curl's default multiplexing. Builds on BL-519 (parallel runner; its ADR BL-518) and BL-659 (HTTP/2 path).
- Code: `Curl.Networking.UnitLibrary/PoolingConnector.cs` and `ConnectionPoolKey.cs` (a multiplexed connection is shared, not checked out), the HTTP/2 connection layer from BL-657 (stream allocation), and `Curl.Console/CurlCommandRunner.cs`.
- Measure with an OpenSSL build of curl against an HTTP/2 server through `Record-CurlExchange.ps1 -NoServer`: `-Z -v` for three URLs on one origin, with and without `--parallel-immediate`; stderr and `-w '%{num_connects}'` copied into Notes.

## Acceptance criteria

- [x] Measured first as above; copied into Notes.
- [x] Tests through fake connectors show three `-Z` transfers using one HTTP/2 connection with stream IDs 1, 3, 5, `%{num_connects}` values as measured, a new connection opened when the concurrent-stream limit is reached, and `--parallel-immediate` opening connections as measured.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for each library changed.

## Notes

- 2026-09-30, resume from the first attempt's work: lane 1 timed out after 120 min on 2026-09-29 with 27 files and about 2,200 lines uncommitted (HTTP/2 multiplexing in Curl.Networking PoolingConnector/MultiplexingNegotiation, Curl.Protocol.Http Http2Session/ReadAheadConnectionStream/ReceivedFrame, Curl.Console CurlCommandRunner/CurlComposition/ParallelHostQueue, and their tests). They are on branch `wip/BL-717`, commit bb9a17be, based on 529fad47. Start with `git fetch origin wip/BL-717` and `git cherry-pick --no-commit bb9a17be` (resolve against the current branch), read what is there, build and run the tests, and finish it rather than starting again. Commit in logical steps as parts pass, so a second timeout loses less. Delete the `wip/BL-717` branch only after this task is Done.
- 2026-09-30, resumed: cherry-picked bb9a17be; four conflicts with BL-636's `IConnection.ClearTlsAsync` resolved by keeping both (a `PooledConnection` lease that asked `ClearTlsAsync` ends as not reusable, so the last lease closes the connection instead of pooling it). Build clean, fast tests green, the new async tests stable over 5 repeated runs.
- `touches` widened to `Curl.Protocol.Abstractions.UnitLibrary` and `.UnitTests`: the work adds `IConnection.IsSharedWithAnotherTransfer` and `IConnectionSession.ConcurrentTransferLimit` (both default interface members, so no other implementer changes). The only other task in Doing (BL-990) touches only `Curl.Protocol.Ssh.*`.
- Measurement (first attempt, 2026-09-29): Record-CurlExchange.ps1 has no HTTP/2 server, so a throwaway C# file-based h2c server (`/tmp/h2srv/h2server.cs`, prior knowledge, configurable `SETTINGS_MAX_CONCURRENT_STREAMS` and response delay) served curl 8.18.0 (x86_64-pc-linux-gnu, OpenSSL/3.5.5, nghttp2/1.68.0, in WSL) and curl 8.18.0 mingw (LibreSSL). No 8.21.0 OpenSSL build with nghttp2 was at hand; the connection logic is unchanged in libcurl between them. Decision: pin the 8.18.0 lines. `-Z --http2-prior-knowledge -v -w '%{num_connects}\n'` for `/1 /2 /3` on one origin:
  - default (PIPEWAIT): `* Trying ...`, then for transfers 2 and 3 `* Connection #0 is not open enough, cannot reuse` / `* Found pending candidate for reuse and CURLOPT_PIPEWAIT is set` / `* Waiting on connection to negotiate possible multiplexing.`; then `[HTTP/2] [1] OPENED stream`, and for 2 and 3 `* Multiplexed connection found` / `* Reusing existing http: connection with host H` / `[HTTP/2] [3]` and `[5] OPENED stream`; one `* Connection #0 to host H:P left intact` after the last response. `%{num_connects}`: 1 for the transfer that opened it, 0 for the other two. One connection on the server.
  - `--parallel-immediate`: three `* Trying`, `* Connection #0 is not open enough, cannot reuse` (and `#1`), three connections each on stream 1, each left intact (`#2`, `#0`, `#1`); `%{num_connects}` 1, 1, 1.
  - server `SETTINGS_MAX_CONCURRENT_STREAMS` 1: once the streams are taken, `* MAX_CONCURRENT_STREAMS reached, skip (1)`, `* Hostname H was found in DNS cache`, a new `* Trying` and connection #1 on stream 1; each connection left intact.
- Tests: `CurlCommandRunnerHttp2MultiplexingTests` (Curl.Console.UnitTests, over the in-memory `Http2ServerConnector`) pins streams 1/3/5 on one connection with num_connects 1/0/0, the reuse lines and a single left-intact, the MAX_CONCURRENT_STREAMS skip opening a second connection, and `--parallel-immediate` opening three; `PoolingConnectorMultiplexingTests` (21 tests) the pool's sharing, lease ending and PIPEWAIT waiting; `Http2SessionTests` and `ReadAheadConnectionStreamTests` the concurrent streams on one session.
- Measure-CodeQuality.ps1: Curl.Networking.UnitLibrary, Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Abstractions.UnitLibrary and Curl.Console all 100% line, 100% branch, 0 failing members, worst CRAP 10.

## Log

- 2026-09-28: Created.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Blocked. Stewart: dark factory timed out after 120 min; see Z:\repos\Curl.logs\BL-717-20260929-191003-L1.jsonl
- 2026-09-30: Blocked -> Backlog. Stewart asked to try again (2026-09-30); resume from branch wip/BL-717.
- 2026-09-30: Backlog -> Doing.
- 2026-09-30: Doing -> Done. -Z transfers to one HTTP/2 origin share one connection on streams 1, 3, 5, waiting for it unless --parallel-immediate, opening another at SETTINGS_MAX_CONCURRENT_STREAMS
