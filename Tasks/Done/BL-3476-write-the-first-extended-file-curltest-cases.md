---
id: BL-3476
title: Write the first Extended FILE curltest cases
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-3474]
touches: [Curl.Conformance.UnitTests/CurlTestCases/Extended/FILE]
requirement: none
created: 2026-10-24
completed: 2026-10-25
---
# BL-3476 — Write the first Extended FILE curltest cases

## Goal

`Curl.Conformance.UnitTests/CurlTestCases/Extended/FILE/` holds 3 to 5 of our own FILE cases, numbered from `ext2001`, each passing through Curl.

## Context

Stewart (2026-10-24) approved our own `.curltest` cases per protocol, beside the upstream copies in `Original/`. BL-3474 makes the runner read `Extended/FILE/` with the number block `ext2001` to `ext2999`; read `Extended/README.md` for the format and run environment. Pick behaviour that upstream's `Original/` cases for FILE do not already cover (search `Original/` by the `<keywords>` and `<server>` of its cases), including the protocol's secure variant where Curl's harness has a server for it. Measure real curl before pinning any expected bytes (`Record-CurlExchange.ps1`, or the reference curl the gap office uses); never guess curl's output.

## Acceptance criteria

- [x] `Extended/FILE/` holds 3 to 5 `ext<number>.curltest` files, numbered within `ext2001` to `ext2999`, in upstream's `tests/data` format.
- [x] Each case's `<info><keywords>` names FILE and the behaviour it checks, and the case's comment or this task's Notes says how its expected output was measured from real curl.
- [x] No case duplicates the behaviour of an upstream case in `Original/` (Notes list the upstream cases searched).
- [x] `UpstreamConformanceTests.ExtendedCases_RunThroughCurl_AllPass` passes, and the fast tests of `Curl.Conformance.UnitTests` pass.
- [x] If a case finds a real Curl bug, the case stays out until a task (filed with the bug, linked in Notes) fixes it; this task still finishes with the cases that pass.

## Notes

- Cases ext2001 (-r -4), ext2002 (-r 7-), ext2003 (percent-encoded space in path), ext2004 (-w size_download), ext2005 (-r past end, exit 36). Each was measured with real curl 8.22.0 (Schannel, Windows) against file://localhost; the cases' header comments say so.
- Upstream FILE cases searched (every Original/test*.curltest with keyword FILE, 42 cases, e.g. 200-205, 231, 288, 390, 1016-1020, 1220, 1440, 1659, 2071, 3203): only 1016 uses -r (X-Y form); none covers suffix or open-ended ranges, percent-encoded paths, size_download or range-past-end.
- FILE has no secure variant. Multi-range (-r 2-3,6-7) was left out: real curl printed only "34", a quirk not worth pinning. No Curl bug found.

## Log

- 2026-10-24: Created.
- 2026-10-25: Backlog -> Doing. Criteria at claim: 5d51bb13 3201854f b9d1ec5d ab8cce71 30cd2836.
- 2026-10-25: Doing -> Done. Five Extended FILE cases ext2001-ext2005 pass through Curl
