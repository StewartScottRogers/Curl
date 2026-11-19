---
id: BL-5551
title: Let a Libcurl port reach CurlCode, RtspRequest, WebSocket flags and callback delegates without extra Curl.Api usings
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Libcurl/Libcurl.csproj, Libcurl/README.md]
requirement: none
created: 2026-11-19
completed: 2026-11-19
---
# BL-5551 — Let a Libcurl port reach CurlCode, RtspRequest, WebSocket flags and callback delegates without extra Curl.Api usings

## Goal

A C program ported to `Libcurl` can use `CurlCode`, `CurlWebSocketFlags`, `RtspRequest` and the callback delegates without the four or five unexpected `using Curl.Api.*` lines it needs today.

## Context

From bug report `2026-11-19T0642-Curl-bug-report.md`, finding 3 (Low), first bullet. Build tested: commit `0afbd10e3b`, `Libcurl 0.2.0`.

`CurlCode`, `CurlWebSocketFlags`, `RtspRequest` and the callback delegates live in `Curl.Api.*` namespaces, not `Libcurl.*`, so a port needs several unexpected `using` lines.

Choose one approach and record it in an ADR (decided by Claude under Stewart's delegation):

- the `Libcurl` package ships implicit usings for those namespaces (a `build/Libcurl.props` adding `<Using Include="..." />` items), so a consuming project sees them with no edits;
- the README's first sample lists every namespace a port needs, under a "Porting from C" heading;
- moving or forwarding the types (a breaking change to `Curl.Api`; weigh it against the others).

No new package reference may be added.

## Acceptance criteria

- [x] An ADR records the chosen approach and why.
- [x] A project referencing the packed `Libcurl` package and using `CurlCode`, `CurlWebSocketFlags`, `RtspRequest` and a callback delegate compiles with only the usings the ADR says a port needs, and `Libcurl/README.md` lists them.
- [x] A test pins that list (the README sample test, if `Libcurl` has one).
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- Chose implicit usings via `Libcurl/build/Libcurl.props` (ADR-1306); packed under `build/` (verified with dotnet pack). Test `PortingUsingsTests` pins README list to the props and compiles without the usings. Production code unchanged, so no quality run.

## Log

- 2026-11-19: Created from bug report 2026-11-19T0642-Curl-bug-report.md, finding 3 (namespaces).
- 2026-11-19: Backlog -> Doing. Criteria at claim: ad5bb533 693f0239 3a0ecd3a 3d836d5e.
- 2026-11-19: Doing -> Done. Libcurl package ships implicit usings for the Curl.Api namespaces a port needs (ADR-1306)
