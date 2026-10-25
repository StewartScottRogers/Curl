---
id: BL-3483
title: Write the first Extended POP3 curltest cases
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-3474]
touches: [Curl.Conformance.UnitTests/CurlTestCases/Extended/POP3]
requirement: none
created: 2026-10-24
completed: 2026-10-25
---
# BL-3483 — Write the first Extended POP3 curltest cases

## Goal

`Curl.Conformance.UnitTests/CurlTestCases/Extended/POP3/` holds 3 to 5 of our own POP3 cases, numbered from `ext9001`, each passing through Curl.

## Context

Stewart (2026-10-24) approved our own `.curltest` cases per protocol, beside the upstream copies in `Original/`. BL-3474 makes the runner read `Extended/POP3/` with the number block `ext9001` to `ext9999`; read `Extended/README.md` for the format and run environment. Pick behaviour that upstream's `Original/` cases for POP3 do not already cover (search `Original/` by the `<keywords>` and `<server>` of its cases), including the protocol's secure variant where Curl's harness has a server for it. Measure real curl before pinning any expected bytes (`Record-CurlExchange.ps1`, or the reference curl the gap office uses); never guess curl's output.

## Acceptance criteria

- [x] `Extended/POP3/` holds 3 to 5 `ext<number>.curltest` files, numbered within `ext9001` to `ext9999`, in upstream's `tests/data` format.
- [x] Each case's `<info><keywords>` names POP3 and the behaviour it checks, and the case's comment or this task's Notes says how its expected output was measured from real curl.
- [x] No case duplicates the behaviour of an upstream case in `Original/` (Notes list the upstream cases searched).
- [x] `UpstreamConformanceTests.ExtendedCases_RunThroughCurl_AllPass` passes, and the fast tests of `Curl.Conformance.UnitTests` pass.
- [x] If a case finds a real Curl bug, the case stays out until a task (filed with the bug, linked in Notes) fixes it; this task still finishes with the cases that pass.

## Notes

Four cases ext9001 to ext9004, each measured by running the same command line through real curl 8.22.0; upstream cases searched are listed in each case. ext9004 pins only the exit code because the message carries a timing.

## Log

- 2026-10-24: Created.
- 2026-10-25: Backlog -> Doing. Criteria at claim: cec95dec 36d50374 b9d1ec5d ab8cce71 30cd2836.
- 2026-10-25: Doing -> Done. Four Extended POP3 cases pass
