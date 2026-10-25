---
id: BL-3488
title: Write the first Extended TELNET curltest cases
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-3474]
touches: [Curl.Conformance.UnitTests/CurlTestCases/Extended/TELNET]
requirement: none
created: 2026-10-24
completed: 2026-10-25
---
# BL-3488 — Write the first Extended TELNET curltest cases

## Goal

`Curl.Conformance.UnitTests/CurlTestCases/Extended/TELNET/` holds 3 to 5 of our own TELNET cases, numbered from `ext14001`, each passing through Curl.

## Context

Stewart (2026-10-24) approved our own `.curltest` cases per protocol, beside the upstream copies in `Original/`. BL-3474 makes the runner read `Extended/TELNET/` with the number block `ext14001` to `ext14999`; read `Extended/README.md` for the format and run environment. Pick behaviour that upstream's `Original/` cases for TELNET do not already cover (search `Original/` by the `<keywords>` and `<server>` of its cases), including the protocol's secure variant where Curl's harness has a server for it. Measure real curl before pinning any expected bytes (`Record-CurlExchange.ps1`, or the reference curl the gap office uses); never guess curl's output.

## Acceptance criteria

- [x] `Extended/TELNET/` holds 3 to 5 `ext<number>.curltest` files, numbered within `ext14001` to `ext14999`, in upstream's `tests/data` format.
- [x] Each case's `<info><keywords>` names TELNET and the behaviour it checks, and the case's comment or this task's Notes says how its expected output was measured from real curl.
- [x] No case duplicates the behaviour of an upstream case in `Original/` (Notes list the upstream cases searched).
- [x] `UpstreamConformanceTests.ExtendedCases_RunThroughCurl_AllPass` passes, and the fast tests of `Curl.Conformance.UnitTests` pass.
- [x] If a case finds a real Curl bug, the case stays out until a task (filed with the bug, linked in Notes) fixes it; this task still finishes with the cases that pass.

## Notes

- Cases ext14001 to ext14004: URL without host (exit 3), port above 65535 (exit 3), unknown `-t` option name (exit 49), `-t WS=80` without WIDTHxHEIGHT (exit 49). Each case comment records that its exit code and -w text were measured from real curl 8.22.0; the two `-t` cases were measured against a local PowerShell TcpListener, since curl checks options only after connecting.
- Upstream cases searched in Original/ for telnet: test1326, test1327, test1452, test1462, test1548, test1714, test445; none checks these. No secure TELNET variant exists, so none added. No Curl bug found.
- Removed Extended/TELNET/.gitkeep now the folder holds cases.

## Log

- 2026-10-24: Created.
- 2026-10-25: Backlog -> Doing. Criteria at claim: 70b38437 eeebac25 b9d1ec5d ab8cce71 30cd2836.
- 2026-10-25: Doing -> Done. Four Extended TELNET cases ext14001-ext14004 pass through Curl
