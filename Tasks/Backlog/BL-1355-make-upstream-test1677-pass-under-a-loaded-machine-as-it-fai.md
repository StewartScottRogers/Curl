---
id: BL-1355
title: Make upstream test1677 pass under a loaded machine, as it failed twice in lane integration
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Conformance.UnitTests]
requirement: none
created: 2026-10-03
completed:
---
# BL-1355 — Make upstream test1677 pass under a loaded machine, as it failed twice in lane integration

## Goal

Upstream case `test1677` in `Curl.Conformance.UnitTests` passes reliably while up to nine dark factory lanes build and test on the same machine.

## Context

- On 2026-10-03 lane 1 could not integrate BL-1333 (an FTP-only change) because the fast tests failed twice on `Curl.Conformance.UnitTests: test1677`. Lane 7 then ran the same change's fast tests twice with test1677 passing, so the failure depends on machine load, not on BL-1333.
- `UpstreamTestData/test1677.rawhttp` is an HTTP POST whose server reply carries both `content-length: 0` and `transfer-encoding: chunked`, served with `writedelay: 500` so the chunks arrive in separate packets. A timing-dependent loopback server under load is the likely cause: find how the conformance runner applies `writedelay` and what timeout or read assumption breaks when a write arrives late.

## Acceptance criteria

- [ ] The cause of the intermittent test1677 failure is named under Notes, with the file and line.
- [ ] The fix makes test1677 independent of machine load (no larger sleep or timeout as the only change).
- [ ] `dotnet test Curl.Conformance.UnitTests --filter "TestCategory!=Integration"` passes.

## Notes

## Log

- 2026-10-03: Created.
