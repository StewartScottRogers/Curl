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
completed:
---

## Goal

The differential responder answers every header, cookie and Content-Disposition route of the tester's server.py byte for byte.

## Context

Reference: `Z:\repos\Curl.difftest\2026-10-22\server.py` (outside the repo, read only, never run Python). Port each route on top of the core from BL-3244 (request reader, `resp`-equivalent response writer with the fixed `Date: Thu, 01 Jan 2026 00:00:00 GMT`, route registry); one self-registering responder file per route under the folder this task touches, and one test file per route over in-memory streams pinning the exact response bytes, including whether the connection closes afterwards (server.py `return True`). Parent: BL-3232 (split for size on 2026-10-23: server.py has about 150 routes, too many for one run).

Routes: `/setcookie`, `/cookies2` (`uni=é` sent as Latin-1), `/dupheaders`, `/manyhdrs`, `/bighdr`, `/hugehdr`, `/hdr-count`, `/hsts`, `/sethsts`, `/altsvc`, `/refresh`, `/cd`, `/cdbad`, `/ct-cd-star`, `/cd-nofile`, `/cd-semicolon`, `/cd-reserved`, `/cd-colon`, `/cd-long`, `/cd-backslash`, and the prefix route for paths starting `/name:`, `/q?`, `/CON` or `/trail.` (answers `named`). Header text is encoded as Latin-1 like server.py.

## Acceptance criteria

- [ ] Every route listed under Context has a responder file under the folder this task touches, and a unit test pins its response bytes.
- [ ] Projects build clean with -warnaserror and their fast tests pass on Windows; every test is platform-neutral (Linux and macOS CI).
- [ ] Measure-CodeQuality.ps1 reports 0 failing members for Curl.Conformance.Differential.UnitLibrary.

## Notes

- Filed by BL-3232's lane-1 run on 2026-10-23 when it split BL-3232 for size.

## Log

- 2026-10-23: Created.
