---
id: BL-3286
title: Port proto2_servers.py's DICT, Gopher, Telnet and RTSP servers to the differential runner
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-3284]
touches: [Curl.Conformance.Differential.UnitLibrary/Protocols/Text, Curl.Conformance.Differential.UnitTests/Protocols/Text]
requirement: none
created: 2026-10-23
completed:
---
# BL-3286 — Port proto2_servers.py's DICT, Gopher, Telnet and RTSP servers to the differential runner

## Goal

DICT (18400), Gopher (18410), Telnet (18420) and RTSP (18430) responders port the matching classes of `proto2_servers.py` byte for byte.

## Context

Reference scripts: `Z:\repos\Curl.difftest\2026-10-22\proto2_servers.py` (read only, never run Python). Parent: BL-3234. Reuse BL-3284's line reader, shared wire log and CliProtocols host; responders over a `Stream` in the UnitLibrary, one file per server, tested with in-memory streams (ADR-0782). Telnet's option negotiation bytes must match the Python class exactly.

## Acceptance criteria

- [ ] DICT, Gopher, Telnet and RTSP responders send the same bytes and log the same wire lines as the Python classes, each covered by unit tests.
- [ ] Projects build clean with -warnaserror and their fast tests pass on Windows; every test is platform-neutral (Linux and macOS CI).
- [ ] Measure-CodeQuality.ps1 reports 0 failing members for Curl.Conformance.Differential.UnitLibrary.

## Notes

- Split out of BL-3234 by its lane-3 run on 2026-10-23.

## Log

- 2026-10-23: Created.
