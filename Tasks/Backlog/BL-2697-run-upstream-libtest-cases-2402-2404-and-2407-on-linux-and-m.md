---
id: BL-2697
title: Run upstream libtest cases 2402, 2404 and 2407 on Linux and macOS and hold those that pass on the ratchet
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Conformance.UnitTests/PassingUpstreamCases.txt]
requirement: none
created: 2026-10-17
completed:
---
# BL-2697 — Run upstream libtest cases 2402, 2404 and 2407 on Linux and macOS and hold those that pass on the ratchet

## Goal

Upstream cases 2402, 2404 and 2407 (lib2402, lib2404, lib2405 over HTTP/2) are measured on Linux and macOS, and each that passes there is held on the ratchet in a way that does not fail on Windows.

## Context

Found by BL-2550, which ported lib2402, lib2404 and lib2405. On Windows all three are skipped (`Curl lacks the feature http/2`): `Curl.Conformance.UnitLibrary/UpstreamCurlPlatform.cs` lists `http/2` off Windows only, so lanes, which test only on Windows, cannot see whether they pass. 2407 also needs the `wakeup` feature question BL-2661 decides. Read a CI run's log for the three cases on Linux and macOS (or run them on a Linux or macOS machine), and check how the ratchet treats a listed case that is skipped on one platform before adding any.

## Acceptance criteria

- [ ] Each of cases 2402, 2404 and 2407 is recorded under Notes as passing or failing on Linux and macOS, with the differing lines for a failing one filed as a task with the board script.
- [ ] Those that pass on Linux and macOS are held on the ratchet without making Windows, Linux or macOS CI red.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-10-17: Created.
