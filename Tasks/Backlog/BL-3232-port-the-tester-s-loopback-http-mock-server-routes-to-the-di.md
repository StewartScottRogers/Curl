---
id: BL-3232
title: Port the tester's loopback HTTP mock server routes to the differential runner
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-3231]
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

## Log

- 2026-10-22: Created.
