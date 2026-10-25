---
id: BL-3481
title: Write the first Extended LDAP curltest cases
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-3474]
touches: [Curl.Conformance.UnitTests/CurlTestCases/Extended/LDAP]
requirement: none
created: 2026-10-24
completed: 2026-10-25
---
# BL-3481 — Write the first Extended LDAP curltest cases

## Goal

`Curl.Conformance.UnitTests/CurlTestCases/Extended/LDAP/` holds 3 to 5 of our own LDAP cases, numbered from `ext7001`, each passing through Curl.

## Context

Stewart (2026-10-24) approved our own `.curltest` cases per protocol, beside the upstream copies in `Original/`. BL-3474 makes the runner read `Extended/LDAP/` with the number block `ext7001` to `ext7999`; read `Extended/README.md` for the format and run environment. Pick behaviour that upstream's `Original/` cases for LDAP do not already cover (search `Original/` by the `<keywords>` and `<server>` of its cases), including the protocol's secure variant where Curl's harness has a server for it. Measure real curl before pinning any expected bytes (`Record-CurlExchange.ps1`, or the reference curl the gap office uses); never guess curl's output.

## Acceptance criteria

- [x] `Extended/LDAP/` holds 3 to 5 `ext<number>.curltest` files, numbered within `ext7001` to `ext7999`, in upstream's `tests/data` format.
- [x] Each case's `<info><keywords>` names LDAP and the behaviour it checks, and the case's comment or this task's Notes says how its expected output was measured from real curl.
- [x] No case duplicates the behaviour of an upstream case in `Original/` (Notes list the upstream cases searched).
- [x] `UpstreamConformanceTests.ExtendedCases_RunThroughCurl_AllPass` passes, and the fast tests of `Curl.Conformance.UnitTests` pass.
- [x] If a case finds a real Curl bug, the case stays out until a task (filed with the bug, linked in Notes) fixes it; this task still finishes with the cases that pass.

## Notes

Curl's harness has no LDAP server, so the four cases (ext7001 to ext7004) need none: bad URL (exit 3), `--proto -ldap` (exit 1), refused connection for ldap:// and ldaps:// on %NOLISTENPORT (exit 7). Expected output measured with real curl 8.22.0 (Schannel, WinLDAP) using the same command lines and `-w`. The connect message carries a timing, so only the exit code is pinned. Upstream cases searched in `Original/`: test445, test1462, test1538 (none runs an ldap:// transfer). Fast tests of Curl.Conformance.UnitTests pass (4374 passed).

## Log

- 2026-10-24: Created.
- 2026-10-25: Backlog -> Doing. Criteria at claim: 91717831 741b9850 b9d1ec5d ab8cce71 30cd2836.
- 2026-10-25: Doing -> Done. Four server-less LDAP cases ext7001-ext7004 pass
