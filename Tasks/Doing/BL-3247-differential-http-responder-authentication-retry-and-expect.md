---
id: BL-3247
title: Differential HTTP responder: authentication, retry and expect routes
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-3244]
touches: [Curl.Conformance.Differential.UnitLibrary/Http/Routes/Auth, Curl.Conformance.Differential.UnitTests/Http/Routes/Auth]
requirement: none
created: 2026-10-23
completed:
---

## Goal

The differential responder answers every authentication, retry and expect route of the tester's server.py, including its per-run state.

## Context

Reference: `Z:\repos\Curl.difftest\2026-10-22\server.py` (outside the repo, read only, never run Python). Port each route on top of the core from BL-3244 (request reader, `resp`-equivalent response writer with the fixed `Date: Thu, 01 Jan 2026 00:00:00 GMT`, route registry); one self-registering responder file per route under the folder this task touches, and one test file per route over in-memory streams pinning the exact response bytes, including whether the connection closes afterwards (server.py `return True`). Parent: BL-3232 (split for size on 2026-10-23: server.py has about 150 routes, too many for one run).

Routes: `/needauth` (Basic u:p), `/digest` (MD5, qop auth, nonce abc), `/digest256` (SHA-256), `/digeststale` (counter, stale=true from the 2nd request), `/ntlmish`, `/authall`, `/proxyauth`, `/need-proxy-auth-too`, `/retry` (503 twice then 200), `/retry500`, `/expect417`. Counters live in the core's per-run state, which the core's reset clears (server.py `STATE`). MD5 and SHA-256 come from `System.Security.Cryptography`.

## Acceptance criteria

- [ ] Every route listed under Context has a responder file under the folder this task touches, and a unit test pins its response bytes.
- [ ] Projects build clean with -warnaserror and their fast tests pass on Windows; every test is platform-neutral (Linux and macOS CI).
- [ ] Measure-CodeQuality.ps1 reports 0 failing members for Curl.Conformance.Differential.UnitLibrary.

## Notes

- Filed by BL-3232's lane-1 run on 2026-10-23 when it split BL-3232 for size.

## Log

- 2026-10-23: Created.
- 2026-10-23: Backlog -> Doing. Criteria at claim: 6b474468 ae951636 5e81fa96.
