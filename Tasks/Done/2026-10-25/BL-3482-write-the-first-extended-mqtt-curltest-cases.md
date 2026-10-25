---
id: BL-3482
title: Write the first Extended MQTT curltest cases
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-3474]
touches: [Curl.Conformance.UnitTests/CurlTestCases/Extended/MQTT]
requirement: none
created: 2026-10-24
completed: 2026-10-25
---
# BL-3482 — Write the first Extended MQTT curltest cases

## Goal

`Curl.Conformance.UnitTests/CurlTestCases/Extended/MQTT/` holds 3 to 5 of our own MQTT cases, numbered from `ext8001`, each passing through Curl.

## Context

Stewart (2026-10-24) approved our own `.curltest` cases per protocol, beside the upstream copies in `Original/`. BL-3474 makes the runner read `Extended/MQTT/` with the number block `ext8001` to `ext8999`; read `Extended/README.md` for the format and run environment. Pick behaviour that upstream's `Original/` cases for MQTT do not already cover (search `Original/` by the `<keywords>` and `<server>` of its cases), including the protocol's secure variant where Curl's harness has a server for it. Measure real curl before pinning any expected bytes (`Record-CurlExchange.ps1`, or the reference curl the gap office uses); never guess curl's output.

## Acceptance criteria

- [x] `Extended/MQTT/` holds 3 to 5 `ext<number>.curltest` files, numbered within `ext8001` to `ext8999`, in upstream's `tests/data` format.
- [x] Each case's `<info><keywords>` names MQTT and the behaviour it checks, and the case's comment or this task's Notes says how its expected output was measured from real curl.
- [x] No case duplicates the behaviour of an upstream case in `Original/` (Notes list the upstream cases searched).
- [x] `UpstreamConformanceTests.ExtendedCases_RunThroughCurl_AllPass` passes, and the fast tests of `Curl.Conformance.UnitTests` pass.
- [x] If a case finds a real Curl bug, the case stays out until a task (filed with the bug, linked in Notes) fixes it; this task still finishes with the cases that pass.

## Notes

- Four offline cases ext8001 to ext8004 (no-host, mqtts no-host, out-of-range port, mqtts connect refused). Expected output measured by running real curl 8.22.0 (Schannel) on the same command lines. Upstream cases searched: test1132, 1190-1196, 1198, 1199, 1916, 1917, 2109, 2200-2207, 3017, 3018, 445; none covers these. No MQTT broker case: pinning one needs the harness broker.
- Fast tests of Curl.Conformance.UnitTests: 4374 passed, 0 failed.

## Log

- 2026-10-24: Created.
- 2026-10-25: Backlog -> Doing. Criteria at claim: 429d4659 7f3ef782 b9d1ec5d ab8cce71 30cd2836.
- 2026-10-25: Doing -> Done. Four Extended MQTT curltest cases ext8001-ext8004 pass through Curl
