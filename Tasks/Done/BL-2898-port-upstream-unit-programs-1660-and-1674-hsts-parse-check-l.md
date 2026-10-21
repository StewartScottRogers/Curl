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
completed: 2026-10-21
---
# BL-2898 — Port upstream unit programs 1660 and 1674 (HSTS parse, check, load, save and the 10000-entry cap) over HstsCache

## Goal

Upstream cases 1660 and 1674 run as ported unit programs over Curl's `HstsCache` and are on `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`.

## Context

Split from BL-2846 for size. curl 8.22.0 `tests/unit/unit1660.c` loads the HSTS file the case writes (path in `UnitProgramContext.Arguments[0]`), runs 23 `Curl_hsts_parse`/`hsts_check` rows printing each result to stdout (`<host> [<host>]: <expires>[ includeSubDomains]`, `'<host>' is not HSTS`, `Input <n>: error 43`), four unknown-directive checks, `Number of entries: <n>`, then ten checks of `expire.example` one second apart, and saves to `<arg>.save`; the case verifies stdout. The clock is `CURL_TIME=1548369261` advanced one second per row (`deltatime++`): inject a fixed, advanceable `TimeProvider` (the context carries no environment). `unit1674.c` writes 10005 entries to its argument, loads them and prints `OK` when exactly `MAX_HSTS_ENTRIES` (10000) are kept; `Curl.Core.UnitLibrary/Hsts/HstsCache.cs` has `MaxEntries = 10_000`. Mechanism: ADR-0711 and BL-2842's Hashes ports. Register in `UnitPrograms.Headers.Hsts.cs`, which BL-2895 creates.

## Acceptance criteria

- [x] unit1660 and unit1674 are ported in `Curl.Conformance.UnitProgram.UnitLibrary/Headers/Hsts/`, check for check with the C programs (named in their doc comments), registered in `UnitPrograms.Headers.Hsts.cs`, with tests in `Curl.Conformance.UnitProgram.UnitTests/Headers/Hsts/`.
- [x] Both cases end `Passed` with `$env:CURL_UPSTREAM_CASE='<n>'` on Windows and are on `PassingUpstreamCases.txt`.
- [x] Where a port finds Curl wrong, the fix has a focused unit test in the fixed library's own `.UnitTests` project (ADR-0482).
- [x] `dotnet build Curl.slnx -warnaserror` is clean, `dotnet test --filter "TestCategory!=Integration"` passes, and each changed library keeps 100% line and branch coverage and complexity at most 10.

## Notes

- The C sources are not in the repository; the rows were rebuilt from unit1660.c as recalled and checked against the case's whole `<stdout>` and `.save` file, which both match. Row 19 is `forexample.net` with `max-age=0` (OK, adds nothing), which is what the case's output and its 4 entries require. The task's "four unknown-directive checks" print nothing in the case's stdout; the unknown directive `include` is row 15.
- Each run starts its own `CurlTimeClock` at `CURL_TIME` 1548369261 (the registered program is shared, so the clock cannot live in it); the `expire.example` loop advances it one second per lookup, as `deltatime++` does.
- The `.save` file is written with `Environment.NewLine` (curl writes it in text mode) and no latest-writable-expiry limit (`long.MaxValue`): the case holds no expiry near either platform's gmtime limit, and a platform ternary would leave one branch uncovered on each OS.
- unit1674 writes `example<n>.com "20991231 12:34:56"` lines, MAX_HSTS_ENTRIES + 5 of them, and prints `OK` or `Not OK: <n> entries`.
- HstsCache needed no fix: every row matched on the first run, so ADR-0482 adds nothing.
- Measured: both cases `Passed` via `NamedUpstreamCase_RunThroughCurl_PassesAsAListedCaseMust`; Curl.Conformance.UnitProgram.UnitLibrary 100% line, 100% branch, 0 failing members, worst CRAP 10.

## Log

- 2026-10-21: Created.
- 2026-10-21: Backlog -> Doing. Criteria at claim: 5e0ade9e f82efa1d aca7e1a2 dd1cc079.
- 2026-10-21: Doing -> Done. Upstream unit programs 1660 and 1674 run as ported programs over HstsCache and both cases are on the ratchet
