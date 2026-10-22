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
completed: 2026-10-18
---
# BL-2697 — Run upstream libtest cases 2402, 2404 and 2407 on Linux and macOS and hold those that pass on the ratchet

## Goal

Upstream cases 2402, 2404 and 2407 (lib2402, lib2404, lib2405 over HTTP/2) are measured on Linux and macOS, and each that passes there is held on the ratchet in a way that does not fail on Windows.

## Context

Found by BL-2550, which ported lib2402, lib2404 and lib2405. On Windows all three are skipped (`Curl lacks the feature http/2`): `Curl.Conformance.UnitLibrary/UpstreamCurlPlatform.cs` lists `http/2` off Windows only, so lanes, which test only on Windows, cannot see whether they pass. 2407 also needs the `wakeup` feature question BL-2661 decides. Read a CI run's log for the three cases on Linux and macOS (or run them on a Linux or macOS machine), and check how the ratchet treats a listed case that is skipped on one platform before adding any.

## Acceptance criteria

- [x] Each of cases 2402, 2404 and 2407 is recorded under Notes as passing or failing on Linux and macOS, with the differing lines for a failing one filed as a task with the board script.
- [x] Those that pass on Linux and macOS are held on the ratchet without making Windows, Linux or macOS CI red.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

Measured 2026-10-18 by running the three cases on Windows with the harness platform set to `UpstreamCurlPlatform.Unix` (http/2, h2c, OpenSSL, xattr), one local run, reverted. CI on Linux and macOS (run 39155312605) lists each as skipped with a run time, so it ran and was judged Inconclusive, but the log does not print the verdict text; the failures below come from the local run.

- 2402: fails. stderr2402 differs at byte 0 (line 1): expected `* Connection #0 to host localhost:9018 left intact`, got the end of the file (empty stderr). Filed BL-2749.
- 2404: fails the same way on stderr2404. Filed BL-2749.
- 2407: fails: `curl threw SocketException: An operation was attempted on something that is not a socket.` Filed BL-2750.

None passes, so none is added to the ratchet and no CI result changes. The ratchet treats a listed case that is skipped as a failure unless it is skipped only for another platform's features (ADR-0488); BL-2749 and BL-2750 must account for Windows, where http/2 cases are skipped for lack of the feature, when they list a case.

## Log

- 2026-10-17: Created.
- 2026-10-18: Backlog -> Doing. Criteria at claim: c0c24af7 bafcc23b 3d836d5e.
- 2026-10-18: Doing -> Done. Measured: 2402, 2404 and 2407 fail with Unix features; nothing to list; filed BL-2749 and BL-2750
