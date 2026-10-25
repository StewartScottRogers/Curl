---
id: BL-3480
title: Write the first Extended IMAP curltest cases
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-3474]
touches: [Curl.Conformance.UnitTests/CurlTestCases/Extended/IMAP]
requirement: none
created: 2026-10-24
completed:
---
# BL-3480 — Write the first Extended IMAP curltest cases

## Goal

`Curl.Conformance.UnitTests/CurlTestCases/Extended/IMAP/` holds 3 to 5 of our own IMAP cases, numbered from `ext6001`, each passing through Curl.

## Context

Stewart (2026-10-24) approved our own `.curltest` cases per protocol, beside the upstream copies in `Original/`. BL-3474 makes the runner read `Extended/IMAP/` with the number block `ext6001` to `ext6999`; read `Extended/README.md` for the format and run environment. Pick behaviour that upstream's `Original/` cases for IMAP do not already cover (search `Original/` by the `<keywords>` and `<server>` of its cases), including the protocol's secure variant where Curl's harness has a server for it. Measure real curl before pinning any expected bytes (`Record-CurlExchange.ps1`, or the reference curl the gap office uses); never guess curl's output.

## Acceptance criteria

- [ ] `Extended/IMAP/` holds 3 to 5 `ext<number>.curltest` files, numbered within `ext6001` to `ext6999`, in upstream's `tests/data` format.
- [ ] Each case's `<info><keywords>` names IMAP and the behaviour it checks, and the case's comment or this task's Notes says how its expected output was measured from real curl.
- [ ] No case duplicates the behaviour of an upstream case in `Original/` (Notes list the upstream cases searched).
- [ ] `UpstreamConformanceTests.ExtendedCases_RunThroughCurl_AllPass` passes, and the fast tests of `Curl.Conformance.UnitTests` pass.
- [ ] If a case finds a real Curl bug, the case stays out until a task (filed with the bug, linked in Notes) fixes it; this task still finishes with the cases that pass.

## Notes

## Log

- 2026-10-24: Created.
- 2026-10-25: Backlog -> Doing. Criteria at claim: 50331f15 0186207d b9d1ec5d ab8cce71 30cd2836.
