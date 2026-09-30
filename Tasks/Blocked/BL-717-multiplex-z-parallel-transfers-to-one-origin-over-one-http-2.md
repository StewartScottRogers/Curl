---
id: BL-717
title: Multiplex -Z parallel transfers to one origin over one HTTP/2 connection
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-519, BL-659]
touches: [Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests, Curl.Networking.UnitLibrary, Curl.Networking.UnitTests, Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-717 — Multiplex -Z parallel transfers to one origin over one HTTP/2 connection

## Goal

With `-Z`, transfers to the same HTTP/2 origin share one connection as concurrent streams, as curl 8.21.0 multiplexes them (up to the server's `SETTINGS_MAX_CONCURRENT_STREAMS`, waiting for the first connection when `--parallel-immediate` is not given), and `%{num_connects}` and `-v`'s connection-reuse lines say so.

## Context

- Standing rule (root `CLAUDE.md`, "Decisions", 2026-09-28): a complete reimplementation includes curl's default multiplexing. Builds on BL-519 (parallel runner; its ADR BL-518) and BL-659 (HTTP/2 path).
- Code: `Curl.Networking.UnitLibrary/PoolingConnector.cs` and `ConnectionPoolKey.cs` (a multiplexed connection is shared, not checked out), the HTTP/2 connection layer from BL-657 (stream allocation), and `Curl.Console/CurlCommandRunner.cs`.
- Measure with an OpenSSL build of curl against an HTTP/2 server through `Record-CurlExchange.ps1 -NoServer`: `-Z -v` for three URLs on one origin, with and without `--parallel-immediate`; stderr and `-w '%{num_connects}'` copied into Notes.

## Acceptance criteria

- [ ] Measured first as above; copied into Notes.
- [ ] Tests through fake connectors show three `-Z` transfers using one HTTP/2 connection with stream IDs 1, 3, 5, `%{num_connects}` values as measured, a new connection opened when the concurrent-stream limit is reached, and `--parallel-immediate` opening connections as measured.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for each library changed.

## Notes

## Log

- 2026-09-28: Created.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Blocked. Stewart: dark factory timed out after 120 min; see Z:\repos\Curl.logs\BL-717-20260929-191003-L1.jsonl
