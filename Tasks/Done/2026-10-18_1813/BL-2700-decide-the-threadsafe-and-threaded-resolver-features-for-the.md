---
id: BL-2700
title: Decide the threadsafe and threaded-resolver features for the upstream case runner so case 3026 runs
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.UnitLibrary, Curl.Conformance.UnitTests]
requirement: none
created: 2026-10-17
completed: 2026-10-18
---
# BL-2700 — Decide the threadsafe and threaded-resolver features for the upstream case runner so case 3026 runs

## Goal

Upstream case 3026 (lib3026, `curl_global_init` from 100 threads at once) runs through the case runner instead of being skipped, and holds the ratchet when it passes.

## Context

Found by BL-2551, which ported lib3026 (`Curl.Conformance.LibTest.UnitLibrary/Lib3026.cs`). Measured on Windows: `test3026 is skipped: Curl lacks the feature threadsafe`. The case needs `<features>threadsafe threaded-resolver</features>`; `Curl.Conformance.UnitLibrary/UpstreamCurlPlatform.cs` lists neither for Curl. Decide (ADR, decided by Claude under Stewart's delegation) whether Curl, being managed code with a lock-protected `CurlGlobal`, reports `threadsafe` (and so whether `curl -V` and `curl_version_info` should list it), and whether `threaded-resolver` is true of Curl's resolver; then make the runner and Curl's feature list agree. The port's check reads `CURL_VERSION_THREADSAFE` from `curl_version_info`, so the engine's `VersionInfo.Features` must carry `threadsafe` for the case to pass.

## Acceptance criteria

- [x] An ADR records whether Curl has the `threadsafe` and `threaded-resolver` features and why.
- [x] Case 3026 runs through the runner on Windows without being skipped, or the ADR says why it stays skipped.
- [x] If it runs and passes it is in `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- Decision (ADR-0695): `UpstreamCurlPlatform` reports `threadsafe` and `threaded-resolver` on every platform. Curl's
  `curl -V` already lists `threadsafe` and `ManagedCurlVersion` already sets `CURL_VERSION_THREADSAFE`, so no engine
  change was needed. `runtests.pl` derives `threaded-resolver` from `AsynchDNS` without `c-ares`, which Curl's line
  and both reference builds show.
- Measured on Windows: test3026 passes and is on the ratchet. test506 (`!threaded-resolver`) is now skipped, as
  `runtests.pl` skips it for the reference curl, so it left the ratchet.
- Fast tests all green (Curl.Conformance.UnitTests 3513 passed, 350 skipped); Measure-CodeQuality for
  Curl.Conformance.UnitLibrary: 0 failing members.

## Log

- 2026-10-17: Created.
- 2026-10-18: Backlog -> Doing. Criteria at claim: 2f800a4f 118cbc50 2bf5540d 3d836d5e.
- 2026-10-18: Doing -> Done. The case runner reports threadsafe and threaded-resolver (ADR-0695); test3026 passes and is on the ratchet
