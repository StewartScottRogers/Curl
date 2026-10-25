---
id: BL-3484
title: Write the first Extended RTSP curltest cases
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-3474]
touches: [Curl.Conformance.UnitTests/CurlTestCases/Extended/RTSP]
requirement: none
created: 2026-10-24
completed: 2026-10-25
---
# BL-3484 — Write the first Extended RTSP curltest cases

## Goal

`Curl.Conformance.UnitTests/CurlTestCases/Extended/RTSP/` holds 3 to 5 of our own RTSP cases, numbered from `ext10001`, each passing through Curl.

## Context

Stewart (2026-10-24) approved our own `.curltest` cases per protocol, beside the upstream copies in `Original/`. BL-3474 makes the runner read `Extended/RTSP/` with the number block `ext10001` to `ext10999`; read `Extended/README.md` for the format and run environment. Pick behaviour that upstream's `Original/` cases for RTSP do not already cover (search `Original/` by the `<keywords>` and `<server>` of its cases), including the protocol's secure variant where Curl's harness has a server for it. Measure real curl before pinning any expected bytes (`Record-CurlExchange.ps1`, or the reference curl the gap office uses); never guess curl's output.

## Acceptance criteria

- [x] `Extended/RTSP/` holds 3 to 5 `ext<number>.curltest` files, numbered within `ext10001` to `ext10999`, in upstream's `tests/data` format.
- [x] Each case's `<info><keywords>` names RTSP and the behaviour it checks, and the case's comment or this task's Notes says how its expected output was measured from real curl.
- [x] No case duplicates the behaviour of an upstream case in `Original/` (Notes list the upstream cases searched).
- [x] `UpstreamConformanceTests.ExtendedCases_RunThroughCurl_AllPass` passes, and the fast tests of `Curl.Conformance.UnitTests` pass.
- [x] If a case finds a real Curl bug, the case stays out until a task (filed with the bug, linked in Notes) fixes it; this task still finishes with the cases that pass.

## Notes

- Four server-less cases ext10001 to ext10004 (rtsp:// and rtsps:// without host, port above 65535, space in path); expected exit code and -w text measured from real curl 8.22.0 with the same command lines. Chosen server-less because the harness RTSP server cases upstream all need lib tools. Upstream cases searched: test1400, 1405-1407, 1420, 1481, 1538, 3100, 445, 567-572, 577, 689 (none checks URL rejection). Rtsps has no harness server, so only its URL rejection is pinned.

## Log

- 2026-10-24: Created.
- 2026-10-25: Backlog -> Doing. Criteria at claim: b5e8a074 0f6771d7 b9d1ec5d ab8cce71 30cd2836.
- 2026-10-25: Doing -> Done. Four Extended RTSP cases ext10001-ext10004 pass through Curl
