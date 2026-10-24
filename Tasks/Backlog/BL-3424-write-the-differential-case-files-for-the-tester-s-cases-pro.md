---
id: BL-3424
title: Write the differential case files for the tester's cases_proto.py cases
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-3233, BL-3235, BL-3284, BL-3285, BL-3286, BL-3287, BL-3288, BL-3334]
touches: [Curl.Conformance.Differential.UnitLibrary/Cases/CliProtocols/Proto, Curl.Conformance.Differential.UnitTests/Protocols/Proto]
requirement: none
created: 2026-10-23
completed:
---
# BL-3424 — Write the differential case files for the tester's cases_proto.py cases

## Goal

The tester's `cases_proto.py` cases (about 130) exist as differential case files under `Cases/CliProtocols/Proto`.

## Context

Reference: `Z:\repos\Curl.difftest\2026-10-22\cases_proto.py` (Python is a reference only; never commit or run it). Server ports from BL-3284 to BL-3287 are in place. Generate the case JSON with a PowerShell script, not by hand. Parent: BL-3234, BL-3217.

## Acceptance criteria

- [ ] One case file per `cases_proto.py` case exists under `Curl.Conformance.Differential.UnitLibrary/Cases/CliProtocols/Proto`, with the same name, arguments and expected server behaviour as the tester's case.
- [ ] A unit test in `Curl.Conformance.Differential.UnitTests/Protocols/Proto` loads every case file and checks the count and that each parses; it is platform-neutral.
- [ ] Projects build clean with -warnaserror and their fast tests pass on Windows.
- [ ] Measure-CodeQuality.ps1 reports 0 failing members for Curl.Conformance.Differential.UnitLibrary.

## Notes

- Split from BL-3234 (over its cost cap) on 2026-10-23.

## Log

- 2026-10-23: Created.
