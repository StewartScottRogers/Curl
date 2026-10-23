---
id: BL-3236
title: Replay the tester's Libcurl URL, printf and escape scenarios (scen.json) in the differential runner
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-3233]
touches: [Curl.Conformance.Differential.UnitLibrary/Libcurl/Scenarios, Curl.Conformance.Differential.UnitTests/Libcurl/Scenarios, Curl.Conformance.Differential.IntegrationTests/LibcurlScenarios]
requirement: none
created: 2026-10-22
completed:
---
# BL-3236 — Replay the tester's Libcurl URL, printf and escape scenarios (scen.json) in the differential runner

## Goal

The differential runner replays the tester's Libcurl scenarios in `scen.json` (3,289: URL API `U`, printf `P`, escape `E`, `N`, `D`, version `V`, `S`) against Curl's Libcurl and the reference libcurl 8.22.0 and reports each difference.

## Context

The tester's harness and data are in `Z:\repos\Curl.difftest\2026-10-22\` (outside the repo; its Python scripts are a reference only - never commit or run Python). Parent task: BL-3217 (Stewart's 2026-10-22 prioritised findings list, "CI differential harness"); the runner's ADR comes from BL-3231. The tester's C# driver is the `LibTest` folder (`Program.cs` and its modes); its output is `lib_summary.txt` and `lib_diff.txt`. Decide how the reference libcurl DLL is loaded beside Curl's (the ADR from BL-3231 or a new one). Broken IDN in the reference build is a named exclusion.

## Acceptance criteria

- [ ] Every scen.json scenario kind replays against both libraries and one run's report is recorded under Notes, each new difference filed.
- [ ] Projects build clean with -warnaserror and their fast tests pass on Windows; every test is platform-neutral (Linux and macOS CI).
- [ ] Measure-CodeQuality.ps1 reports 0 failing members for Curl.Conformance.Differential.UnitLibrary.

## Notes

- Split out of BL-3217 by its lane-3 run on 2026-10-22.

## Log

- 2026-10-22: Created.
