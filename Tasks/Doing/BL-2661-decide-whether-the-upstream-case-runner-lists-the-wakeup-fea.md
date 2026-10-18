---
id: BL-2661
title: Decide whether the upstream case runner lists the wakeup feature now that libtest programs run over Libcurl (cases 1564, 1565)
priority: Normal
assignee: Claude
pipeline: docs
depends-on: []
touches: [Curl.Conformance.UnitLibrary, Curl.Conformance.UnitTests]
requirement: none
created: 2026-10-17
completed:
---
# BL-2661 — Decide whether the upstream case runner lists the wakeup feature now that libtest programs run over Libcurl (cases 1564, 1565)

## Goal

An ADR decides whether `UpstreamCurlPlatform` lists the `wakeup` feature now that libtest programs run over Libcurl, which has `curl_multi_wakeup`, and the runner acts on it, so upstream cases 1564 and 1565 either run (and, passing, hold the ratchet) or stay skipped for a recorded reason.

## Context

Found by BL-2542, which ported lib1564 and lib1565: both are skipped with `Curl lacks the feature wakeup`. `Curl.Conformance.UnitLibrary/UpstreamCurlPlatform.cs` keeps `wakeup` off as a libcurl API feature a command-line tool does not have; `form-api` and `headers-api` were turned on for the same reason (BL-2593). Other cases that need `wakeup` would run too, so check them all.

## Acceptance criteria

- [ ] An ADR marked "Decided by Claude under Stewart's delegation" records the decision and its row is in `Documentation/Planning/Decisions/README.md`.
- [ ] When `wakeup` is listed, upstream cases 1564 and 1565 run through the runner and those that pass are added to `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`; each that fails is filed as a task.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-10-17: Created.
- 2026-10-18: Backlog -> Doing. Criteria at claim: feccc413 756a3894 3d836d5e.
