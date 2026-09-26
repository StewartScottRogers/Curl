---
id: BL-232
title: Wire -i, -I, -f, --fail-with-body and --fail-early in Curl.Console
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-176, BL-190, BL-231]
touches: [Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-09-26
completed:
---
# BL-232 — Wire -i, -I, -f, --fail-with-body and --fail-early in Curl.Console

## Goal

`-i`, `-I`, `-f`, `--fail-with-body` and `--fail-early` behave in `Curl.Console` as in curl 8.21.0.

## Context

- Filed from the Phase 1 HTTP plan (protocol-architect, 2026-09-26), item W3. Upstream references: https://curl.se/docs/manpage.html and https://curl.se/libcurl/c/libcurl-errors.html; behaviour measured on curl 8.21.0.
- -D is wired already (BL-121).
- Where a criterion says *measured*, run curl 8.21.0 - the mingw build `/mingw64/bin/curl`, first on PATH, which is the Windows reference (ADR-0009 and the BL-153 ADR) - against a loopback server (the BL-165 recording script `Record-CurlExchange.ps1` once it exists), record the exact command and the bytes it produced in `Notes`, then pin them in a test. Never pin text that was not measured.

## Acceptance criteria

- [ ] `-i` with `-D` and `-I` with `-o` match measured stdout, file contents and exit.
- [ ] `-f` on 404 exits 22 with `curl: (22) The requested URL returned error: 404` on stderr under `-sS`/default as measured.
- [ ] `--fail-early` stops the remaining URLs as measured.
- [ ] `dotnet build Curl.Console -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes and no new test needs `TestCategory=Integration`; `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Console`.

## Notes

- Plan item: W3 in the Phase 1 HTTP plan (2026-09-26); plan keys in this file were replaced by their task IDs.

## Log

- 2026-09-26: Created.
- 2026-09-26: Backlog -> Doing.
