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
completed:
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

- [ ] An ADR records the chosen approach and why.
- [ ] A project referencing the packed `Libcurl` package and using `CurlCode`, `CurlWebSocketFlags`, `RtspRequest` and a callback delegate compiles with only the usings the ADR says a port needs, and `Libcurl/README.md` lists them.
- [ ] A test pins that list (the README sample test, if `Libcurl` has one).
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-19: Created from bug report 2026-11-19T0642-Curl-bug-report.md, finding 3 (namespaces).
