---
id: BL-3098
title: Pin the DoH resolver's NXDOMAIN and CNAME-only outcome and trace lines as curl 8.22.0 measured, and record them in ADR-0738
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-3011, BL-3012]
touches: [Curl.Networking.UnitTests/DohDnsResolverTraceTests.AnswerDecoding.cs, Documentation/Planning/Decisions/ADR-0738-doh-answers-decode-nxdomain-and-cap-ttls-as-curl-8-22-0-does.md]
requirement: none
created: 2026-10-22
completed: 2026-10-22
---
# BL-3098 — Pin the DoH resolver's NXDOMAIN and CNAME-only outcome and trace lines as curl 8.22.0 measured, and record them in ADR-0738

## Goal

The DoH resolver's exit code and `--trace-config doh` lines for an NXDOMAIN answer and a CNAME-only answer match curl 8.22.0 as measured, are pinned in `Curl.Networking.UnitTests` and are recorded in ADR-0738.

## Context

Resolver half of BL-2969. BL-3011 landed the code and BL-3012 amended ADR-0738 with BL-2970's measurements (in `Tasks/Deferred/BL-2970-*.md` Notes): NXDOMAIN prints `* [DoH] [DoH] [A] error decoding response: No such name` and exits 6; a CNAME-only answer prints `* [DoH] DoH request complete, 0 to go` and exits 6. Check `Curl.Networking.UnitTests/DohDnsResolverTraceTests.AnswerDecoding.cs` and the ADR for each; add what is missing, tick the rest.

## Acceptance criteria

- [x] `Curl.Networking.UnitTests` pins the resolver's outcome and `--trace-config doh` lines for an NXDOMAIN answer and for a CNAME-only answer as curl 8.22.0 measured.
- [x] ADR-0738 records those exit codes and trace lines.
- [x] `dotnet test --filter "TestCategory!=Integration"` passes for `Curl.Networking.UnitTests`.

## Notes

- Split from BL-2969 (over its cost cap); BL-2969's second criterion.
- 2026-10-22 (lane 2): Nothing to add. BL-3011 and BL-3012 already landed both pins: `ResolveAsync_AnNxdomainAnswer_ReportsNoSuchNameAndResolvesNothing` (the one line `[DoH] [DoH] [A] error decoding response: No such name`, no address) and `ResolveAsync_ACnameOnlyAnswer_ReportsNoLineAndResolvesNothing` (no resolver line, no address; `DoH request complete, 0 to go` is a sub-transfer line outside the resolver). In both cases the resolver returns no address, so the engine exits 6 (ADR-0152). ADR-0738's measured table records both lines and exit 6. Curl.Networking.UnitTests fast run: 3372 passed, 0 failed, 29 skipped.

## Log

- 2026-10-22: Created.
- 2026-10-22: Backlog -> Doing. Criteria at claim: 6208194f 06955b45 af2d943f.
- 2026-10-22: Doing -> Done. NXDOMAIN and CNAME-only DoH outcomes and trace lines are pinned in Curl.Networking.UnitTests and recorded in ADR-0738, as curl 8.22.0 measured
