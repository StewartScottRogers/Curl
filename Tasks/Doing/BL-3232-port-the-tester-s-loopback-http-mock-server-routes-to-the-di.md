---
id: BL-3232
title: Port the tester's loopback HTTP mock server routes to the differential runner
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-3231, BL-3244, BL-3245, BL-3246, BL-3247, BL-3248, BL-3249, BL-3250]
touches: [Curl.Conformance.Differential.UnitLibrary/Http, Curl.Conformance.Differential.UnitTests/Http]
requirement: none
created: 2026-10-22
completed:
---
# BL-3232 — Port the tester's loopback HTTP mock server routes to the differential runner

## Goal

The differential runner's UnitLibrary holds a stream-based HTTP/1.1 responder that answers every route the tester's `server.py` serves (`/echo`, `/chunked`, `/gziptrail`, `/te-gzip-chunked`, redirects, cookies, auth and the rest), one self-registering file per route, unit tested over in-memory streams.

## Context

The tester's harness and data are in `Z:\repos\Curl.difftest\2026-10-22\` (outside the repo; its Python scripts are a reference only - never commit or run Python). Parent task: BL-3217 (Stewart's 2026-10-22 prioritised findings list, "CI differential harness"); the runner's ADR comes from BL-3231. Port `server.py` (288 lines, about 120 route branches) in C#, matching its response bytes exactly, including its fixed `Date: Thu, 01 Jan 2026 00:00:00 GMT`. The socket host that serves it is BL-3233's; keep this task to request parsing and response bytes so it is fully covered by unit tests.

## Acceptance criteria

- [ ] Every route in server.py has a responder file under Curl.Conformance.Differential.UnitLibrary/Http, and a unit test pins each route's response bytes.
- [ ] Projects build clean with -warnaserror and their fast tests pass on Windows; every test is platform-neutral (Linux and macOS CI).
- [ ] Measure-CodeQuality.ps1 reports 0 failing members for Curl.Conformance.Differential.UnitLibrary.

## Notes

- Split out of BL-3217 by its lane-3 run on 2026-10-22.
- 2026-10-23 (lane 1): split for size before any code. server.py has about 150 routes; one responder file and one test per route does not fit one run. Filed BL-3244 (core: request reader, response writer, registry, state, control responder, fall-through routes) and six route groups on disjoint subfolders under Http/Routes: BL-3245 redirects, BL-3246 encodings, BL-3247 auth/retry/expect, BL-3248 malformed, BL-3249 headers/cookies/Content-Disposition, BL-3250 bodies/range/timing. Every route of server.py is named in exactly one of them. Once they are Done this task only checks the criteria across the whole Http folder.

## Log

- 2026-10-22: Created.
- 2026-10-23: Backlog -> Doing. Criteria at claim: 52267cc6 ae951636 5e81fa96.
- 2026-10-23: Doing -> Backlog. Split for size: waits on BL-3244 to BL-3250 (responder core and six route groups)
- 2026-10-23: Backlog -> Doing. Criteria at claim: 52267cc6 ae951636 5e81fa96.
