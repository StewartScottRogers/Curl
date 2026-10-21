---
id: BL-2981
title: Split DohDnsResolver.cs and DohTraceLines.cs so the DoH tasks can run in parallel
priority: High
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Networking.UnitLibrary/DohDnsResolver.cs, Curl.Networking.UnitLibrary/DohTraceLines.cs, Curl.Networking.UnitTests/DohDnsResolverTraceTests.cs]
requirement: none
created: 2026-10-21
completed:
---
# BL-2981 — Split DohDnsResolver.cs and DohTraceLines.cs so the DoH tasks can run in parallel

## Goal

`task-board.ps1 hotspots` no longer lists `DohDnsResolver.cs`, `DohTraceLines.cs` or
`DohDnsResolverTraceTests.cs`, because the waiting DoH tasks each touch a file of their own.

## Context

On 2026-10-21 six of nine lanes sat idle ("every ready task overlaps one in Doing"), and the board's
hotspots were `Curl.Networking.UnitLibrary/DohDnsResolver.cs` (7 tasks: BL-2923, BL-2937, BL-2969,
BL-2970, BL-2972, BL-2973, BL-2979), `DohTraceLines.cs` (6) and
`Curl.Networking.UnitTests/DohDnsResolverTraceTests.cs` (5). CLAUDE.md: a file many tasks touch is
split as its own High task rather than waited on (Stewart, 2026-10-21: "always function in a highly
parallel fashion").

## Acceptance criteria

- [ ] `DohDnsResolver.cs` and `DohTraceLines.cs` are split, by partial class or by extracting types,
      along the lines the waiting tasks work on: request encoding, answer decoding (NXDOMAIN, CNAME,
      TTL), HTTP status failures, sub-transfer trace lines, and HTTPS-RR trace gating. Behaviour is
      unchanged.
- [ ] `DohDnsResolverTraceTests.cs` is split the same way, so each area's tests are in their own file.
- [ ] Each waiting DoH task's `touches` (BL-2923, BL-2937, BL-2969, BL-2970, BL-2972, BL-2973,
      BL-2979) names only the new files its work lands in, edited in Backlog with the task board's
      rules.
- [ ] `task-board.ps1 hotspots` lists none of the three files; `dotnet build` is clean, the fast
      tests pass and `Measure-CodeQuality.ps1` passes for `Curl.Networking.UnitLibrary`.

## Notes

## Log

- 2026-10-21: Created.
