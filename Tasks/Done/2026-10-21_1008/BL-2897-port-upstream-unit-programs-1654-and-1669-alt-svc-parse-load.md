---
id: BL-2897
title: Port upstream unit programs 1654 and 1669 (alt-svc parse, load, save and the 5000-entry cap) over AltSvcCache
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2895]
touches: [Curl.Conformance.UnitTests/PassingUpstreamCases.txt, Curl.Conformance.UnitProgram.UnitLibrary/UnitPrograms.Headers.AltSvc.cs, Curl.Conformance.UnitProgram.UnitLibrary/Headers/AltSvc, Curl.Conformance.UnitProgram.UnitTests/Headers/AltSvc, Curl.Core.UnitLibrary/AltSvc, Curl.Core.UnitTests/AltSvc]
requirement: none
created: 2026-10-21
completed: 2026-10-21
---
# BL-2897 — Port upstream unit programs 1654 and 1669 (alt-svc parse, load, save and the 5000-entry cap) over AltSvcCache

## Goal

Upstream cases 1654 and 1669 run as ported unit programs over Curl's `AltSvcCache` and are on `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`.

## Context

Split from BL-2846 for size. curl 8.22.0 `tests/unit/unit1654.c` loads the alt-svc file the case writes (path in `UnitProgramContext.Arguments[0]`), checks the entry count after each of 14 `Curl_altsvc_parse` calls (including `clear`, unknown ALPN, bad quotes, missing and illegal ports, unknown extension parameters before `ma` and `persist`), looks one entry up, and saves to `<arg>-out`, which the case verifies byte for byte. The clock is `CURL_TIME=1548369261`: the port injects a fixed `TimeProvider` (the context carries no environment; the C program hard-codes the same epoch in its check). `unit1669.c` loads a file of more than 5000 entries and expects exactly `MAX_ALTSVC_ENTRIES` (5000); Curl's `Curl.Core.UnitLibrary/AltSvc/AltSvcCache.cs` has no such cap today (grep found none), so this task likely adds it, with a focused test in `Curl.Core.UnitTests/AltSvc` (ADR-0482), matching curl 8.22.0's `lib/altsvc.c`. Mechanism: ADR-0711 and BL-2842's Hashes ports. Register in `UnitPrograms.Headers.AltSvc.cs`, which BL-2895 creates.

## Acceptance criteria

- [x] unit1654 and unit1669 are ported in `Curl.Conformance.UnitProgram.UnitLibrary/Headers/AltSvc/`, check for check with the C programs (named in their doc comments), registered in `UnitPrograms.Headers.AltSvc.cs`, with tests in `Curl.Conformance.UnitProgram.UnitTests/Headers/AltSvc/`.
- [x] Both cases end `Passed` with `$env:CURL_UPSTREAM_CASE='<n>'` on Windows and are on `PassingUpstreamCases.txt`.
- [x] Where a port finds Curl wrong (such as the 5000-entry cap), the fix has a focused unit test in the fixed library's own `.UnitTests` project (ADR-0482).
- [x] `dotnet build Curl.slnx -warnaserror` is clean, `dotnet test --filter "TestCategory!=Integration"` passes, and each changed library keeps 100% line and branch coverage and complexity at most 10.

## Notes

- Ports: `Unit1654AltSvcParse` (15 parse rows, `Unit1654AltSvcRow`, then the `params.example` lookup, clear and save to `<file>-out` with `Environment.NewLine`, as `Curl.Console` writes the file) and `Unit1669AltSvcEntryCap`. `CurlTimeClock` is the fixed `CURL_TIME=1548369261` clock; `AltSvcCacheFile` is `Curl_altsvc_load` (a missing file reads nothing, no error, as libcurl). The C program's `fail_if(result)` checks on `Curl_altsvc_parse` have no counterpart: it fails only on out-of-memory and `ApplyHeader` cannot fail.
- Port found two Curl defects, both fixed against curl 8.22.0's `lib/altsvc.c` with focused tests (ADR-0482):
  1. No `MAX_ALTSVC_ENTRIES`: `AltSvcCache.MaxEntries` = 5000; adding to a full cache, from the file or a header, first drops the oldest entry, as `altsvc_append` does (`AltSvcCacheEntryCapTests`).
  2. A quoted parameter value holding `;` (`quoted="a;\"b"; ma=120`) was cut at the `;`, so `ma` was read as part of the next name and lost. `AltSvcHeaderParser` now reads a closed quoted value to its closing quote, `\` escaping, as `curlx_str_quotedword` does. Choice: an unclosed quote keeps the old reading (BL-622 measured `ma="3x` as ma=3 on curl 8.21.0 and a test pins it), so only values whose quote closes change.
- Measured: `Measure-CodeQuality.ps1 -Library Curl.Core.UnitLibrary,Curl.Conformance.UnitProgram.UnitLibrary` - both 100% line and branch; `UnquotedTextStart` was complexity 12 and was split (`QuotedTextEnd`); re-measured Core: 0 failing members.
- Platform: the port's tests use their own temporary folders; the saved file uses the platform line ending, which the case's text-mode verify expects on each platform.

## Log

- 2026-10-21: Created.
- 2026-10-21: Backlog -> Doing. Criteria at claim: 5d46eb8f f82efa1d bee7b905 dd1cc079.
- 2026-10-21: Doing -> Done. Upstream unit1654 and unit1669 pass as ported unit programs over AltSvcCache, which now caps at 5000 entries and reads quoted parameter values to their closing quote
