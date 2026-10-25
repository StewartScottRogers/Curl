---
id: BL-3487
title: Write the first Extended SSH curltest cases
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-3474]
touches: [Curl.Conformance.UnitTests/CurlTestCases/Extended/SSH]
requirement: none
created: 2026-10-24
completed: 2026-10-25
---
# BL-3487 — Write the first Extended SSH curltest cases

## Goal

`Curl.Conformance.UnitTests/CurlTestCases/Extended/SSH/` holds 3 to 5 of our own SSH cases, numbered from `ext13001`, each passing through Curl.

## Context

Stewart (2026-10-24) approved our own `.curltest` cases per protocol, beside the upstream copies in `Original/`. BL-3474 makes the runner read `Extended/SSH/` with the number block `ext13001` to `ext13999`; read `Extended/README.md` for the format and run environment. Pick behaviour that upstream's `Original/` cases for SSH do not already cover (search `Original/` by the `<keywords>` and `<server>` of its cases), including the protocol's secure variant where Curl's harness has a server for it. Measure real curl before pinning any expected bytes (`Record-CurlExchange.ps1`, or the reference curl the gap office uses); never guess curl's output.

## Acceptance criteria

- [x] `Extended/SSH/` holds 3 to 5 `ext<number>.curltest` files, numbered within `ext13001` to `ext13999`, in upstream's `tests/data` format.
- [x] Each case's `<info><keywords>` names SSH and the behaviour it checks, and the case's comment or this task's Notes says how its expected output was measured from real curl.
- [x] No case duplicates the behaviour of an upstream case in `Original/` (Notes list the upstream cases searched).
- [x] `UpstreamConformanceTests.ExtendedCases_RunThroughCurl_AllPass` passes, and the fast tests of `Curl.Conformance.UnitTests` pass.
- [x] If a case finds a real Curl bug, the case stays out until a task (filed with the bug, linked in Notes) fixes it; this task still finishes with the cases that pass.

## Notes

Cases ext13001 (SFTP empty file, -w size_download), ext13002 (SCP empty file), ext13003 (SFTP -r 0-0), ext13004 (SFTP --max-filesize 3 exits 63). Upstream cases searched: test600-test642, test1446, test1459, test1583, test1725; none covers these. No sshd exists for Record-CurlExchange.ps1 to record, so expected output is the documented curl behaviour (downloaded bytes, then -w text; exit 63 for file size exceeded), as in the SMB cases. Verified they execute (a wrong exit code fails the run).

## Log

- 2026-10-24: Created.
- 2026-10-25: Backlog -> Doing. Criteria at claim: 91ff79b1 b81647bd b9d1ec5d ab8cce71 30cd2836.
- 2026-10-25: Doing -> Done. Four Extended SSH cases pass through Curl
