---
id: BL-2743
title: Make upstream case 2405 pass now that the wakeup feature is listed (curl_multi_waitfds, expected exit 0, got 44)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2661]
touches: [Curl.Conformance.LibTest.UnitLibrary, Curl.Conformance.LibTest.UnitTests, Curl.Conformance.UnitTests]
requirement: none
created: 2026-10-18
completed:
---
# BL-2743 — Make upstream case 2405 pass now that the wakeup feature is listed (curl_multi_waitfds, expected exit 0, got 44)

## Goal

Upstream case 2405 (lib2405, `curl_multi_waitfds`) passes through the runner and is added to `PassingUpstreamCases.txt`.

## Context

Found by BL-2661, which listed the `wakeup` feature (ADR-0690): 2405 now runs and fails with `<verify><errorcode>: expected exit code 0, got 44` (`Curl.Conformance.LibTest.UnitLibrary/Lib2405.cs`). Run it with `CURL_UPSTREAM_CASE=2405 dotnet test Curl.Conformance.UnitTests --filter "TestCategory=Manual"`. It is not on the ratchet until it passes.

## Acceptance criteria

- [ ] Case 2405 passes and is listed in `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`.
- [ ] A focused unit test in the fixed library's own `.UnitTests` project covers the fix.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-10-18: Created.
