---
id: BL-2741
title: Run upstream case 1560 (lib1560, the URL API test) to success and hold it on the ratchet
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2731, BL-2732, BL-2733, BL-2734, BL-2735, BL-2736, BL-2737, BL-2738, BL-2739, BL-2740]
touches: [Curl.Conformance.UnitTests/PassingUpstreamCases.txt]
requirement: none
created: 2026-10-18
completed:
---
# BL-2741 — Run upstream case 1560 (lib1560, the URL API test) to success and hold it on the ratchet

## Goal

Upstream case 1560 passes through the case runner (stdout `success`) and is on `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`.

## Context

BL-2652 ported lib1560 (`Lib1560`, `Lib1560Checks`, `Lib1560Tables`) and filed each cause of a differing row as BL-2731 to BL-2740. Once they are Done, run case 1560 (`CURL_UPSTREAM_CASE=1560`); any row still differing is filed as its own task with the board script and named in `depends-on`. The `_WIN32` rows run only on Windows and the `CURLU_PUNYCODE`/`CURLU_PUNY2IDN` rows only with `CURL_TEST_HAVE_CODESET_UTF8`, as upstream's do; check the case passes on Linux and macOS (CI) before holding it, or pin it Windows-only.

## Acceptance criteria

- [ ] Case 1560 passes through the runner and is added to `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`.
- [ ] `dotnet build` is clean and the fast tests pass with no test deleted, skipped or weakened.

## Notes

## Log

- 2026-10-18: Created.
