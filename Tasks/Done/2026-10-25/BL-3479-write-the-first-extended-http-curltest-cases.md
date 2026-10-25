---
id: BL-3479
title: Write the first Extended HTTP curltest cases
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-3474]
touches: [Curl.Conformance.UnitTests/CurlTestCases/Extended/HTTP]
requirement: none
created: 2026-10-24
completed: 2026-10-25
---
# BL-3479 — Write the first Extended HTTP curltest cases

## Goal

`Curl.Conformance.UnitTests/CurlTestCases/Extended/HTTP/` holds 3 to 5 of our own HTTP cases, numbered from `ext5001`, each passing through Curl.

## Context

Stewart (2026-10-24) approved our own `.curltest` cases per protocol, beside the upstream copies in `Original/`. BL-3474 makes the runner read `Extended/HTTP/` with the number block `ext5001` to `ext5999`; read `Extended/README.md` for the format and run environment. Pick behaviour that upstream's `Original/` cases for HTTP do not already cover (search `Original/` by the `<keywords>` and `<server>` of its cases), including the protocol's secure variant where Curl's harness has a server for it. Measure real curl before pinning any expected bytes (`Record-CurlExchange.ps1`, or the reference curl the gap office uses); never guess curl's output.

## Acceptance criteria

- [x] `Extended/HTTP/` holds 3 to 5 `ext<number>.curltest` files, numbered within `ext5001` to `ext5999`, in upstream's `tests/data` format.
- [x] Each case's `<info><keywords>` names HTTP and the behaviour it checks, and the case's comment or this task's Notes says how its expected output was measured from real curl.
- [x] No case duplicates the behaviour of an upstream case in `Original/` (Notes list the upstream cases searched).
- [x] `UpstreamConformanceTests.ExtendedCases_RunThroughCurl_AllPass` passes, and the fast tests of `Curl.Conformance.UnitTests` pass.
- [x] If a case finds a real Curl bug, the case stays out until a task (filed with the bug, linked in Notes) fixes it; this task still finishes with the cases that pass.

## Notes

- Cases: ext5001 (-f on 404: -w errormsg, exitcode, http_code; exit 22), ext5002 (HEAD: size_download 0, size_header 47), ext5003 (-X PATCH -d with a user Content-Type). Each carries a comment saying its expected bytes came from real curl 8.22.0 via Record-CurlExchange.ps1 against a canned loopback server.
- Upstream searched in Original/ (grep over tests): errormsg, exitcode, size_header, size_request, num_headers, header_json, content_type, method, scheme, -X PATCH, max-filesize, url-query, -A "", -H "Name;". None combines errormsg with exitcode under -f, none uses size_header with -I and an unsent body, none overrides Content-Type on a PATCH -d.
- The runner adds --include unless option="no-include", so stdout cases use no-include. crlf="headers" does not convert the request line, so ext5003 uses crlf="yes" nonewline="yes".
- Secure variant: HTTPS cases are left to a later task. No real Curl bug found.
- This worktree's FILE/ext2001 and ext2004 are CRLF in the working tree (index is LF, stale checkout before BL-3476 fix), so the aggregate test fails locally on them only; all HTTP cases pass. CI checks out LF.

## Log

- 2026-10-24: Created.
- 2026-10-25: Backlog -> Doing. Criteria at claim: 575c3ee1 650e9819 b9d1ec5d ab8cce71 30cd2836.
- 2026-10-25: Doing -> Done. three Extended HTTP cases ext5001-ext5003 pass through Curl
