---
id: BL-3485
title: Write the first Extended SMB curltest cases
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-3474]
touches: [Curl.Conformance.UnitTests/CurlTestCases/Extended/SMB]
requirement: none
created: 2026-10-24
completed: 2026-10-25
---
# BL-3485 — Write the first Extended SMB curltest cases

## Goal

`Curl.Conformance.UnitTests/CurlTestCases/Extended/SMB/` holds 3 to 5 of our own SMB cases, numbered from `ext11001`, each passing through Curl.

## Context

Stewart (2026-10-24) approved our own `.curltest` cases per protocol, beside the upstream copies in `Original/`. BL-3474 makes the runner read `Extended/SMB/` with the number block `ext11001` to `ext11999`; read `Extended/README.md` for the format and run environment. Pick behaviour that upstream's `Original/` cases for SMB do not already cover (search `Original/` by the `<keywords>` and `<server>` of its cases), including the protocol's secure variant where Curl's harness has a server for it. Measure real curl before pinning any expected bytes (`Record-CurlExchange.ps1`, or the reference curl the gap office uses); never guess curl's output.

## Acceptance criteria

- [x] `Extended/SMB/` holds 3 to 5 `ext<number>.curltest` files, numbered within `ext11001` to `ext11999`, in upstream's `tests/data` format.
- [x] Each case's `<info><keywords>` names SMB and the behaviour it checks, and the case's comment or this task's Notes says how its expected output was measured from real curl.
- [x] No case duplicates the behaviour of an upstream case in `Original/` (Notes list the upstream cases searched).
- [x] `UpstreamConformanceTests.ExtendedCases_RunThroughCurl_AllPass` passes, and the fast tests of `Curl.Conformance.UnitTests` pass.
- [x] If a case finds a real Curl bug, the case stays out until a task (filed with the bug, linked in Notes) fixes it; this task still finishes with the cases that pass.

## Notes

- Reference curl 8.22.0 (Schannel) has no SMB, so nothing could be recorded; expected bytes are the file the stand-in serves (as upstream test1451). Cases: ext11001 empty file, ext11002 subdirectory path, ext11003 size_download, ext11004 wrong share (exit 78).
- Upstream SMB cases searched in Original/: test1451 (basic) and test1850 (two requests); none overlap.
- `-o` with SMB writes no bytes (real Curl bug); that case is left out, filed as BL-3504.

## Log

- 2026-10-24: Created.
- 2026-10-25: Backlog -> Doing. Criteria at claim: fb7b0806 ef93f4c6 b9d1ec5d ab8cce71 30cd2836.
- 2026-10-25: Doing -> Done. Four Extended SMB cases ext11001-ext11004 pass through Curl; -o bug filed as BL-3504
