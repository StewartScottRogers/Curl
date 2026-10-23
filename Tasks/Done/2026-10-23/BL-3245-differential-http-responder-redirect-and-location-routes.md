---
id: BL-3245
title: Differential HTTP responder: redirect and Location routes
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-3244]
touches: [Curl.Conformance.Differential.UnitLibrary/Http/Routes/Redirects, Curl.Conformance.Differential.UnitTests/Http/Routes/Redirects]
requirement: none
created: 2026-10-23
completed: 2026-10-23
---

## Goal

The differential responder answers every redirect route of the tester's server.py byte for byte.

## Context

Reference: `Z:\repos\Curl.difftest\2026-10-22\server.py` (outside the repo, read only, never run Python). Port each route on top of the core from BL-3244 (request reader, `resp`-equivalent response writer with the fixed `Date: Thu, 01 Jan 2026 00:00:00 GMT`, route registry); one self-registering responder file per route under the folder this task touches, and one test file per route over in-memory streams pinning the exact response bytes, including whether the connection closes afterwards (server.py `return True`). Parent: BL-3232 (split for size on 2026-10-23: server.py has about 150 routes, too many for one run).

Routes: `/r301`..`/r308` (regex `^/r30[1-8]$`), `/rloop`, `/rrel`, `/rabs`, `/rother`, `/rftp`, `/rspace`, `/redir-cookie`, `/loc-empty`, `/loc-missing`, `/loc-multi`, `/loc-query`, `/loc-scheme-rel`, `/loc-utf8` (UTF-8 bytes of `é` sent raw), `/loc-file`, `/loc-dotdot`, `/loc-userinfo`, `/loc-307-body`, `/post-303`, `/auth-redir`. Routes that embed the port (`/rabs`, `/rother`, `/loc-scheme-rel`, `/loc-userinfo`) take it from the responder options the core defines.

## Acceptance criteria

- [x] Every route listed under Context has a responder file under the folder this task touches, and a unit test pins its response bytes.
- [x] Projects build clean with -warnaserror and their fast tests pass on Windows; every test is platform-neutral (Linux and macOS CI).
- [x] Measure-CodeQuality.ps1 reports 0 failing members for Curl.Conformance.Differential.UnitLibrary.

## Notes

- Filed by BL-3232's lane-1 run on 2026-10-23 when it split BL-3232 for size.
- Plan: one abstract `RedirectRoute` (status, headers before `Content-Length: 0`, empty body, keeps the connection) under `Http/Routes/Redirects`, and one self-registering sealed route per server.py line (20 files; `/r301`..`/r308` is `RedirectStatusRoute`, which takes the status from the path). One test file per route sends the path twice (GET, then POST with a query) and then `/other`, pinning both answers byte for byte and that the connection stays open.
- `/loc-utf8`: server.py builds the value with `"/echo?q=é".encode("utf8").decode("latin1")` and then sends the head with `str.encode()` (UTF-8), so the wire bytes are C3 83 C2 A9 (double-encoded), not the raw C3 A9 the Context says. Decided by server.py byte for byte (the Goal): the route holds U+00C3 U+00A9 and the core writer's UTF-8 head reproduces C3 83 C2 A9; the test pins those bytes.
- `/r305` and `/r306` answer `Status` as their reason phrase, as server.py's REASONS table has no entry for them.
- Measure-CodeQuality.ps1 -Library Curl.Conformance.Differential.UnitLibrary: 0 failing members. Solution build -warnaserror clean; fast tests green (45 assemblies, 0 failed). Tests are in-memory only, so platform-neutral for Linux and macOS CI.

## Log

- 2026-10-23: Created.
- 2026-10-23: Backlog -> Doing. Criteria at claim: 6b474468 ae951636 5e81fa96.
- 2026-10-23: Doing -> Done. The differential responder answers all 20 server.py redirect and Location routes byte for byte
