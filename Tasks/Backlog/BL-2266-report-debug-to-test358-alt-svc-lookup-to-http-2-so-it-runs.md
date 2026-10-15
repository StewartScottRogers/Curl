---
id: BL-2266
title: Report Debug to test358 (alt-svc lookup to HTTP/2) so it runs on Linux and macOS
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-2263]
touches: [Curl.Conformance.UnitTests]
requirement: none
created: 2026-10-14
completed:
---
# BL-2266 — Report Debug to test358 (alt-svc lookup to HTTP/2) so it runs on Linux and macOS

## Goal

Case 358 runs on Linux and macOS instead of skipping for the Debug feature.

## Context

Found by BL-2259 (2026-10-14): test358 (HTTP GET translated by alt-svc lookup to HTTP/2 GET) is skipped on Linux with "Curl lacks the feature Debug". BL-2263 reports Debug to 359 and 2406 and makes Curl honour CURL_ALTSVC_HTTP in the harness; 358 needs the same, so it waits for BL-2263. Start at UpstreamConformanceTests.PlatformFor in Curl.Conformance.UnitTests.

## Acceptance criteria

- [ ] PlatformFor reports Debug to 358, with a comment saying why.
- [ ] 358 is in PassingUpstreamCases.txt if it passes on Linux and macOS CI; otherwise its first difference is in Notes with a follow-up task.
## Notes

## Log

- 2026-10-14: Created.
