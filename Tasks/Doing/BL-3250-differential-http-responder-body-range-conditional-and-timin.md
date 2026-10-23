---
id: BL-3250
title: Differential HTTP responder: body, range, conditional and timing routes
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-3244]
touches: [Curl.Conformance.Differential.UnitLibrary/Http/Routes/Bodies, Curl.Conformance.Differential.UnitTests/Http/Routes/Bodies]
requirement: none
created: 2026-10-23
completed:
---

## Goal

The differential responder answers every body, range, conditional and timing route of the tester's server.py.

## Context

Reference: `Z:\repos\Curl.difftest\2026-10-22\server.py` (outside the repo, read only, never run Python). Port each route on top of the core from BL-3244 (request reader, `resp`-equivalent response writer with the fixed `Date: Thu, 01 Jan 2026 00:00:00 GMT`, route registry); one self-registering responder file per route under the folder this task touches, and one test file per route over in-memory streams pinning the exact response bytes, including whether the connection closes afterwards (server.py `return True`). Parent: BL-3232 (split for size on 2026-10-23: server.py has about 150 routes, too many for one run).

Routes: `/utf8`, `/ctype`, `/jsonish`, `/bom`, `/nul`, `/crlf-body`, `/binary`, `/empty`, `/big` (3,000,000 bytes of i % 251), `/range` (parses `Range: bytes=a-b`), `/range416`, `/norange`, `/lm`, `/cond` (If-None-Match), `/early` (103 then 200), `/early100`, `/echo-method`, `/slow` (3 s), `/slowbody` (3 s between halves), `/idle-close` (0.3 s then close), `/close-hdr`. Delays use the injected `TimeProvider` so tests run with a fake clock and never sleep.

## Acceptance criteria

- [ ] Every route listed under Context has a responder file under the folder this task touches, and a unit test pins its response bytes.
- [ ] Projects build clean with -warnaserror and their fast tests pass on Windows; every test is platform-neutral (Linux and macOS CI).
- [ ] Measure-CodeQuality.ps1 reports 0 failing members for Curl.Conformance.Differential.UnitLibrary.

## Notes

- Filed by BL-3232's lane-1 run on 2026-10-23 when it split BL-3232 for size.

## Log

- 2026-10-23: Created.
- 2026-10-23: Backlog -> Doing. Criteria at claim: 6b474468 ae951636 5e81fa96.
