---
id: BL-2898
title: Port upstream unit programs 1660 and 1674 (HSTS parse, check, load, save and the 10000-entry cap) over HstsCache
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2895]
touches: [Curl.Conformance.UnitTests/PassingUpstreamCases.txt, Curl.Conformance.UnitProgram.UnitLibrary/UnitPrograms.Headers.Hsts.cs, Curl.Conformance.UnitProgram.UnitLibrary/Headers/Hsts, Curl.Conformance.UnitProgram.UnitTests/Headers/Hsts, Curl.Core.UnitLibrary/Hsts, Curl.Core.UnitTests/Hsts]
requirement: none
created: 2026-10-21
completed:
---
# BL-2898 — Port upstream unit programs 1660 and 1674 (HSTS parse, check, load, save and the 10000-entry cap) over HstsCache

## Goal

Upstream cases 1660 and 1674 run as ported unit programs over Curl's `HstsCache` and are on `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`.

## Context

Split from BL-2846 for size. curl 8.22.0 `tests/unit/unit1660.c` loads the HSTS file the case writes (path in `UnitProgramContext.Arguments[0]`), runs 23 `Curl_hsts_parse`/`hsts_check` rows printing each result to stdout (`<host> [<host>]: <expires>[ includeSubDomains]`, `'<host>' is not HSTS`, `Input <n>: error 43`), four unknown-directive checks, `Number of entries: <n>`, then ten checks of `expire.example` one second apart, and saves to `<arg>.save`; the case verifies stdout. The clock is `CURL_TIME=1548369261` advanced one second per row (`deltatime++`): inject a fixed, advanceable `TimeProvider` (the context carries no environment). `unit1674.c` writes 10005 entries to its argument, loads them and prints `OK` when exactly `MAX_HSTS_ENTRIES` (10000) are kept; `Curl.Core.UnitLibrary/Hsts/HstsCache.cs` has `MaxEntries = 10_000`. Mechanism: ADR-0711 and BL-2842's Hashes ports. Register in `UnitPrograms.Headers.Hsts.cs`, which BL-2895 creates.

## Acceptance criteria

- [ ] unit1660 and unit1674 are ported in `Curl.Conformance.UnitProgram.UnitLibrary/Headers/Hsts/`, check for check with the C programs (named in their doc comments), registered in `UnitPrograms.Headers.Hsts.cs`, with tests in `Curl.Conformance.UnitProgram.UnitTests/Headers/Hsts/`.
- [ ] Both cases end `Passed` with `$env:CURL_UPSTREAM_CASE='<n>'` on Windows and are on `PassingUpstreamCases.txt`.
- [ ] Where a port finds Curl wrong, the fix has a focused unit test in the fixed library's own `.UnitTests` project (ADR-0482).
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, `dotnet test --filter "TestCategory!=Integration"` passes, and each changed library keeps 100% line and branch coverage and complexity at most 10.

## Notes

## Log

- 2026-10-21: Created.
- 2026-10-21: Backlog -> Doing. Criteria at claim: 5e0ade9e f82efa1d aca7e1a2 dd1cc079.
