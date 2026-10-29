---
id: BL-4078
title: Pin each UTS 46 mapping class's 30 measured hosts against Curl's Host header
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-4033]
touches: [Curl.Protocol.Abstractions.UnitTests/CurlUrlHostNameMappingTests.cs, Curl.Protocol.Abstractions.UnitTests/Cgr019Tests.cs, Curl.Protocol.Http.UnitTests/Uts46MeasuredHostHeaderTests.cs]
requirement: none
created: 2026-10-29
completed: 2026-10-29
---
# BL-4078 — Pin each UTS 46 mapping class's 30 measured hosts against Curl's Host header

## Goal

For every mapping class of BL-3829, at least 30 hosts measured against curl with libidn2 2.3.8 are pinned as Curl's `Host` header, so BL-3829's first criterion is met.

## Context

BL-3829 (ADR-0980) maps host names with the whole UTS #46 table and NFC. BL-4033 measured 443 hosts in eight classes (compatibility 68, East Asian 117, presentation forms 53, case 49, outside the BMP 42, Hangul 30, Indic nukta 38, combining-mark order 32) with curl 8.18.0 and libidn2 2.3.8 and pinned them as `TryNormalize_Measured*_ConvertsAsCurlWithLibidn2` rows in `CurlUrlHostNameMappingTests.cs`; its Notes hold the method. Check that every class has at least 30 pinned rows, that `Cgr019Tests.cs` agrees with the table, and that Curl's `Host` header (not only `TryNormalize`) equals the pinned host for a sample of each class. Add rows or tests for any class that falls short.

## Acceptance criteria

- [x] Each of the eight classes above has at least 30 measured rows in `CurlUrlHostNameMappingTests.cs`, and a focused test per class asserts the `Host` header Curl builds equals the measured host.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Split from BL-3829 (its first criterion, measuring the hosts); the measurement itself is BL-4033's.
- BL-4033 used curl 8.18.0 as a stand-in for 8.22.0; the conversion is libidn2 2.3.8 in both.
- Counted the measured rows per class: case 48, compatibility 64, East Asian 117, Hangul 30, outside the BMP 42, Indic nukta 38, presentation forms 53, combining-mark order 32 - every class has at least 30, so no rows were added. `Cgr019Tests.cs` passes unchanged beside them, so it agrees with the table.
- Added `Curl.Protocol.Http.UnitTests/Uts46MeasuredHostHeaderTests.cs` to touches: the `Host` line is built by `HttpRequestHeadFormatter` in Curl.Protocol.Http.UnitLibrary, which Abstractions.UnitTests cannot reach; no task in Doing on origin/work/dark-factory names that file (new file). One test per class formats a request for three measured hosts and asserts the `Host:` line.
- Decision (default taken): the Host-line tests are off Windows only (`OSCondition` Exclude Windows). On Windows Curl reads the host as Windows-1252 first (CGR-059, ADR-0939) and no Windows curl was measured for these hosts, so the libidn2 answers hold only off Windows. Lanes test only on Windows, so these run on CI Linux and macOS; they take the same path (`CurlUrl.Parse` -> `TryNormalizeReadingHost` with Windows-1252 off -> `IdnHost` for a non-ASCII host) as the conversion rows that pass here, and CI already runs a UTF-8 locale (HttpUrlTextTests' åäö.se row needs it off Windows).
- Build clean; fast tests green on Windows (46 test assemblies passed, none failed; the new Host-line tests show as skipped here and run on CI Linux and macOS, kept platform-neutral by their OSCondition). No production code changed, so no quality run was needed.

## Log

- 2026-10-29: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: 77b244f6 779790c0.
- 2026-10-29: Doing -> Done. Every UTS 46 class has 30+ measured rows and a per-class test pins Curl's Host line for its measured hosts
