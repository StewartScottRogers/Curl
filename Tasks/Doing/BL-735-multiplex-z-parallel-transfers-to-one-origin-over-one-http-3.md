---
id: BL-735
title: Multiplex -Z parallel transfers to one origin over one HTTP/3 connection
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-717, BL-732]
touches: [Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests, Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-735 — Multiplex -Z parallel transfers to one origin over one HTTP/3 connection

## Goal

With `-Z` and HTTP/3, transfers to the same origin share one QUIC connection as concurrent request streams, up to the server's `MAX_STREAMS`, as curl's official build multiplexes them, with `%{num_connects}` and the `-v` reuse lines as measured.

## Context

- Standing rule (root `CLAUDE.md`, "Decisions", 2026-09-28). Builds on BL-717 (the multiplexing rules and pool changes made for HTTP/2) and BL-732 (HTTP/3 transfers).
- Measure with the official curl build against an HTTP/3 server through `Record-CurlExchange.ps1 -NoServer`: `-Z --http3 -v` for three URLs on one origin; stderr and `-w '%{num_connects}'` copied into Notes.

## Acceptance criteria

- [ ] Measured first as above; copied into Notes.
- [ ] Tests with a fake multiplexed connection show three `-Z` transfers on one QUIC connection with client stream IDs 0, 4, 8, `%{num_connects}` as measured, and a new connection when `MAX_STREAMS` is reached.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for each library changed.

## Notes

## Log

- 2026-09-28: Created.
- 2026-10-01: Backlog -> Doing.
