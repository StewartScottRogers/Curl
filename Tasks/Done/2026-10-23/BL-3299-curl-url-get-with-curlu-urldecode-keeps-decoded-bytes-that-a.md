---
id: BL-3299
title: curl_url_get with CURLU_URLDECODE keeps decoded bytes that are not UTF-8 instead of U+FFFD
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Libcurl/Url/CurlUrlHandle.cs, Libcurl/CLAUDE.md, Libcurl.UnitTests/Url/CurlUrlHandleTests.cs, Curl.Conformance.Differential.IntegrationTests/LibcurlScenarios/ManagedLibcurlScenarioTarget.cs, Curl.Engine.UnitLibrary/ManagedCurlUrlHandle.cs, Curl.Engine.UnitTests/ManagedCurlUrlHandleTests.cs]
requirement: none
created: 2026-10-23
completed: 2026-10-23
---
# BL-3299 — curl_url_get with CURLU_URLDECODE keeps decoded bytes that are not UTF-8 instead of U+FFFD

## Goal

curl_url_get with CURLU_URLDECODE returns the decoded bytes even when they are not UTF-8 (%FF gives 0xFF), as libcurl 8.22.0 does.

## Context

Scenarios 1080-1100 (http://a/%FF, %C0%AF, %ED%A0%80): Curl's CurlUrlHandle.Get returns a string, so the decoded bytes come back as U+FFFD (EF BF BD); libcurl returns the raw bytes. The managed API needs a byte form or a lossless mapping; decide in an ADR. Found by the BL-3236 differential replay of the tester's scen.json against libcurl 8.22.0 (Git for Windows ucrt64) on 2026-10-23; rerun with CURL_DIFFERENTIAL_REFERENCE_LIBCURL set (Curl.Conformance.Differential.IntegrationTests/LibcurlScenarios, ADR-0790). Measure the platform libcurl before pinning.

## Acceptance criteria

- [x] A unit test pins the raw bytes for http://a/%FF with CURLU_URLDECODE through the API the C ABI uses.
- [x] The build is clean and the fast tests pass.

## Notes

- The engine already kept the byte: `UrlPartText` decodes `%FF` to U+DCFF (ADR-0697). The loss was `CurlUrlHandle.Get` answering only a string, which the differential runner's managed target wrote as UTF-8. Added `CurlUrlHandle.GetBytes`, the C ABI's raw bytes through `UrlByteText.ToBytes` (ADR-0796); a separate name, as a `Get(out byte[]?)` overload makes existing `out var` callers ambiguous. libcurl's bytes were measured by BL-3236's replay against libcurl 8.22.0 (the task's Context), so no new measurement.
- Tests: `CurlUrlHandleTests.GetBytes_DecodedPercentFF_IsTheRawByteAsLibcurlGivesIt` (http://a/%FF with CURLU_URLDECODE gives 2F FF) and the null case; `ManagedCurlUrlHandleTests.Get_DecodedPathOfPercentFF_IsDcffNotReplacementCharacter`. `ManagedCurlUrlHandle.cs` needed no change.
- Touches widened to `Libcurl/CLAUDE.md`, `Libcurl.UnitTests/Url/CurlUrlHandleTests.cs` and the differential runner's `ManagedLibcurlScenarioTarget.cs` (now reads through `GetBytes`); no task in Doing on origin/work/dark-factory names them.
- Build clean, fast tests green, Measure-CodeQuality -Library Libcurl: 0 failing members. Change is platform-neutral (no paths or OS text).

## Log

- 2026-10-23: Created.
- 2026-10-23: Backlog -> Doing. Criteria at claim: ba969c52 c46ca32a.
- 2026-10-23: Doing -> Done. curl_url_get's byte form (CurlUrlHandle.GetBytes) gives a decoded %FF as the raw byte 0xFF, as libcurl 8.22.0
