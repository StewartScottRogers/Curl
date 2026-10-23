---
id: BL-3249
title: Differential HTTP responder: header, cookie and Content-Disposition routes
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-3244]
touches: [Curl.Conformance.Differential.UnitLibrary/Http/Routes/HeadersCookies, Curl.Conformance.Differential.UnitTests/Http/Routes/HeadersCookies]
requirement: none
created: 2026-10-23
completed: 2026-10-23
---

## Goal

The differential responder answers every header, cookie and Content-Disposition route of the tester's server.py byte for byte.

## Context

Reference: `Z:\repos\Curl.difftest\2026-10-22\server.py` (outside the repo, read only, never run Python). Port each route on top of the core from BL-3244 (request reader, `resp`-equivalent response writer with the fixed `Date: Thu, 01 Jan 2026 00:00:00 GMT`, route registry); one self-registering responder file per route under the folder this task touches, and one test file per route over in-memory streams pinning the exact response bytes, including whether the connection closes afterwards (server.py `return True`). Parent: BL-3232 (split for size on 2026-10-23: server.py has about 150 routes, too many for one run).

Routes: `/setcookie`, `/cookies2` (`uni=é` sent as Latin-1), `/dupheaders`, `/manyhdrs`, `/bighdr`, `/hugehdr`, `/hdr-count`, `/hsts`, `/sethsts`, `/altsvc`, `/refresh`, `/cd`, `/cdbad`, `/ct-cd-star`, `/cd-nofile`, `/cd-semicolon`, `/cd-reserved`, `/cd-colon`, `/cd-long`, `/cd-backslash`, and the prefix route for paths starting `/name:`, `/q?`, `/CON` or `/trail.` (answers `named`). Header text is encoded as Latin-1 like server.py.

## Acceptance criteria

- [x] Every route listed under Context has a responder file under the folder this task touches, and a unit test pins its response bytes.
- [x] Projects build clean with -warnaserror and their fast tests pass on Windows; every test is platform-neutral (Linux and macOS CI).
- [x] Measure-CodeQuality.ps1 reports 0 failing members for Curl.Conformance.Differential.UnitLibrary.

## Notes

- Filed by BL-3232's lane-1 run on 2026-10-23 when it split BL-3232 for size.
- All 21 routes answer through one shared base, `FixedResponseRoute` (a fixed `resp` answer, connection kept: none of these routes `return True` in server.py), with one self-registering file per route; each test sends the path twice and then the path plus `x`, so it pins the bytes, that the connection stays open, and that only that exact path matches.
- `uni=é` in `/cookies2`: Context says it goes out as Latin-1, but server.py's `resp` encodes the head with `str.encode()`, which is UTF-8, and `"...".encode("latin1").decode("latin1")` does nothing. To stay byte for byte with server.py the test pins `C3 A9`, which the core writer already sends. "Header text is encoded as Latin-1" holds only for request decoding.
- The `/q?` prefix never matches a request: server.py splits the path at the first `?` before it routes. It is kept anyway for fidelity, and a direct `Matches` call covers it.
- Platform-neutral: in-memory streams only, no paths, files or OS text, so the Linux and macOS CI criterion is met by the green Windows run.
- Measure-CodeQuality.ps1 -Library Curl.Conformance.Differential.UnitLibrary: 100% line, 100% branch, 0 failing members.

## Log

- 2026-10-23: Created.
- 2026-10-23: Backlog -> Doing. Criteria at claim: 6b474468 ae951636 5e81fa96.
- 2026-10-23: Doing -> Done. Differential responder answers every header, cookie and Content-Disposition route of server.py byte for byte
