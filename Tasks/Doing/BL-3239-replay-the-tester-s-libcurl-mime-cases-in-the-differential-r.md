---
id: BL-3239
title: Replay the tester's Libcurl MIME cases in the differential runner
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-3237]
touches: [Curl.Conformance.Differential.UnitLibrary/Libcurl/Mime, Curl.Conformance.Differential.UnitTests/Libcurl/Mime, Curl.Conformance.Differential.IntegrationTests/LibcurlMime]
requirement: none
created: 2026-10-22
completed:
---
# BL-3239 — Replay the tester's Libcurl MIME cases in the differential runner

## Goal

The differential runner replays the tester's Libcurl MIME cases (`mimefile.json`) and printf inputs (`pf_in.json`) against Curl's Libcurl and libcurl 8.22.0.

## Context

The tester's harness and data are in `Z:\repos\Curl.difftest\2026-10-22\` (outside the repo; its Python scripts are a reference only - never commit or run Python). Parent task: BL-3217 (Stewart's 2026-10-22 prioritised findings list, "CI differential harness"); the runner's ADR comes from BL-3231. Drivers: `LibTest/MimeMode.cs`, `LibTest/PrintfMode.cs`; outputs `mimefile.txt`, `pf_out.txt`, `pf_report.txt`.

## Acceptance criteria

- [ ] The MIME and printf inputs replay against both libraries and one run's report is recorded under Notes, each new difference filed.
- [ ] Projects build clean with -warnaserror and their fast tests pass on Windows; every test is platform-neutral (Linux and macOS CI).
- [ ] Measure-CodeQuality.ps1 reports 0 failing members for Curl.Conformance.Differential.UnitLibrary.

## Notes

- Split out of BL-3217 by its lane-3 run on 2026-10-22.

## Log

- 2026-10-22: Created.
- 2026-10-23: Backlog -> Doing. Criteria at claim: f9ace538 ae951636 5e81fa96.
