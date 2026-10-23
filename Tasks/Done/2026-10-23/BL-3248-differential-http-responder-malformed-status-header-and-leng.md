---
id: BL-3248
title: Differential HTTP responder: malformed status, header and length routes
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-3244]
touches: [Curl.Conformance.Differential.UnitLibrary/Http/Routes/Malformed, Curl.Conformance.Differential.UnitTests/Http/Routes/Malformed]
requirement: none
created: 2026-10-23
completed: 2026-10-23
---

## Goal

The differential responder answers every malformed-response route of the tester's server.py byte for byte.

## Context

Reference: `Z:\repos\Curl.difftest\2026-10-22\server.py` (outside the repo, read only, never run Python). Port each route on top of the core from BL-3244 (request reader, `resp`-equivalent response writer with the fixed `Date: Thu, 01 Jan 2026 00:00:00 GMT`, route registry); one self-registering responder file per route under the folder this task touches, and one test file per route over in-memory streams pinning the exact response bytes, including whether the connection closes afterwards (server.py `return True`). Parent: BL-3232 (split for size on 2026-10-23: server.py has about 150 routes, too many for one run).

Routes (most write raw bytes with no Date header): `/status-nocode`, `/status-noreason`, `/status-ver2`, `/status-ver9`, `/status-lower`, `/status-999`, `/status-2digit`, `/cl-neg`, `/cl-dup-same`, `/cl-dup-diff`, `/cl-and-te`, `/cl-huge`, `/hdr-nospace`, `/hdr-badline`, `/hdr-space-before-colon`, `/folded`, `/lenshort`, `/badstatus`, `/nothing` (writes nothing, closes), `/head-body`, `/304-body`, `/204-cl`, `/closedelim`, `/http10`, `/http10len`, `/nocontent`, `/notmod`.

## Acceptance criteria

- [x] Every route listed under Context has a responder file under the folder this task touches, and a unit test pins its response bytes.
- [x] Projects build clean with -warnaserror and their fast tests pass on Windows; every test is platform-neutral (Linux and macOS CI).
- [x] Measure-CodeQuality.ps1 reports 0 failing members for Curl.Conformance.Differential.UnitLibrary.

## Notes

- Filed by BL-3232's lane-1 run on 2026-10-23 when it split BL-3232 for size.
- Each route is a sealed class over one shared base, `FixedResponseRoute` (in the Malformed folder), holding the exact bytes server.py writes and whether it returns True; the five `resp` routes (`/nocontent`, `/notmod`, `/closedelim`, `/http10`, `/http10len`) build their bytes with `DifferentialHttpResponseWriter.Format`, the rest are byte literals. Chosen as the simplest port: every route here answers the same bytes on every request.
- Each test sends the route two GETs, so the pinned output shows whether the connection closed after the first. Tests use in-memory streams and ASCII only, so they are platform-neutral; Linux and macOS CI criterion met by that and the fast tests green on Windows.
- Measure-CodeQuality.ps1 -Library Curl.Conformance.Differential.UnitLibrary: 0 failing members.

## Log

- 2026-10-23: Created.
- 2026-10-23: Backlog -> Doing. Criteria at claim: 6b474468 ae951636 5e81fa96.
- 2026-10-23: Doing -> Done. All 27 malformed routes ported with byte-pinned tests; build clean, fast tests green, quality 0 failing.
