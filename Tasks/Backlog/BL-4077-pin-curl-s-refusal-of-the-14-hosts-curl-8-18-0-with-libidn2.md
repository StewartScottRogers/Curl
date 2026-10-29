---
id: BL-4077
title: Pin Curl's refusal of the 14 hosts curl 8.18.0 with libidn2 refused
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-4033, BL-4076]
touches: [Curl.Protocol.Abstractions.UnitTests/CurlUrlHostNameMappingTests.cs]
requirement: none
created: 2026-10-29
completed:
---
# BL-4077 — Pin Curl's refusal of the 14 hosts curl 8.18.0 with libidn2 refused

## Goal

Curl refuses the 14 hosts curl with libidn2 2.3.8 refused (exit 3 or 27), with the same exit code, and a test pins each.

## Context

BL-4033 did not pin 14 hosts curl refused: U+2100, U+2101, U+2105, U+2106 and ten Arabic presentation forms in U+FE70..U+FE7F and U+FDFA. Their measurements were in a session scratchpad only, so re-measure each on Linux (method in BL-4033's Notes: `socat` as a fake proxy, `curl -x`, `LC_ALL=C.utf8`) and record the exit code of each in Notes. Then pin what Curl does for each in `CurlUrlHostNameMappingTests.cs`, and fix `CurlUrlHostNameMapping` where Curl accepts or maps one that curl refuses. Validity differences in general are BL-3588's; only these 14 hosts are in scope.

## Acceptance criteria

- [ ] Each of the 14 hosts has its curl exit code recorded in Notes and a test in `CurlUrlHostNameMappingTests.cs` pinning that Curl refuses it the same way.
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Split from BL-3829 (completing its measured-host coverage); the 14 refused hosts are BL-4033's unpinned remainder.

## Log

- 2026-10-29: Created.
