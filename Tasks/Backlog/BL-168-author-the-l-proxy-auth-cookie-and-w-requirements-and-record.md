---
id: BL-168
title: Author the -L, proxy, auth, cookie and -w requirements and record HTTP in the Roadmap
priority: Normal
assignee: Claude
pipeline: docs
depends-on: [BL-167, BL-151]
touches: [Documentation/Product/Requirements.md, Documentation/Planning/Roadmap.md]
requirement: none
created: 2026-09-26
completed:
---
# BL-168 — Author the -L, proxy, auth, cookie and -w requirements and record HTTP in the Roadmap

## Goal

`Documentation/Product/Requirements.md` has FR rows for `-L`, proxies, authentication, cookies and `-w`, and `Documentation/Planning/Roadmap.md` Milestone 1 lists the HTTP work with the exit criterion `curl http(s)://...`.

## Context

- Filed from the Phase 1 HTTP plan (protocol-architect, 2026-09-26), item R3. Upstream references: https://curl.se/docs/manpage.html and https://curl.se/libcurl/c/libcurl-errors.html; behaviour measured on curl 8.21.0.
- Continue numbering after BL-167. Authentication and Cookies are in Milestone 1 per the BL-151 ADR.
- Measured: `-L --max-redirs 0` exit 47 `curl: (47) Maximum (0) redirects followed`; `-p -x` answered 407 exit 7 `curl: (7) CONNECT tunnel failed, response 407`; `-u u:p` sends `Authorization: Basic dTpw` before `User-Agent`.
- `Documentation/Planning/Roadmap.md` "Milestone 1" says today "HTTP and the other Phase 1 option groups are part of Phase 1 but have no tasks yet".
- Where a criterion says *measured*, run curl 8.21.0 - the mingw build `/mingw64/bin/curl`, first on PATH, which is the Windows reference (ADR-0009 and the BL-153 ADR) - against a loopback server (the BL-165 recording script `Record-CurlExchange.ps1` once it exists), record the exact command and the bytes it produced in `Notes`, then pin them in a test. Never pin text that was not measured.

## Acceptance criteria

- [ ] FR rows cover redirect limits, method rewriting and credential rules; proxy selection and environment variables; Basic, Bearer, Digest and `--anyauth`; `-b`, `-c`, `-j`; `-w` variables.
- [ ] `Documentation/Planning/Roadmap.md` "Milestone 1": "Delivers" lists the HTTP handler, command-line groups, Core, Networking, Authentication, Cookies, Output and Console items by task ID; "Exit criteria" adds `curl http(s)://...` end to end with curl's request bytes, output and exit codes; the "no tasks yet" sentence is removed.

## Notes

- Plan item: R3 in the Phase 1 HTTP plan (2026-09-26); plan keys in this file were replaced by their task IDs.

## Log

- 2026-09-26: Created.
