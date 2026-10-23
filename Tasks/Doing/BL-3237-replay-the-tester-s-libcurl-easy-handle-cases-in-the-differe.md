---
id: BL-3237
title: Replay the tester's Libcurl easy-handle cases in the differential runner
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-3233]
touches: [Curl.Conformance.Differential.UnitLibrary/Libcurl/Easy, Curl.Conformance.Differential.UnitTests/Libcurl/Easy, Curl.Conformance.Differential.IntegrationTests/LibcurlEasy]
requirement: none
created: 2026-10-22
completed:
---
# BL-3237 — Replay the tester's Libcurl easy-handle cases in the differential runner

## Goal

The differential runner replays the tester's Libcurl easy-handle cases (`e_in.json`, `h_in.json`, `h_round.json`, `t1.json`) against Curl's Libcurl and libcurl 8.22.0.

## Context

The tester's harness and data are in `Z:\repos\Curl.difftest\2026-10-22\` (outside the repo; its Python scripts are a reference only - never commit or run Python). Parent task: BL-3217 (Stewart's 2026-10-22 prioritised findings list, "CI differential harness"); the runner's ADR comes from BL-3231. Driver: `LibTest/EasyMode.cs`; outputs `easy_report.txt`, `h_out.txt`, `t1.out`. Reuse BL-3236's way of loading the reference DLL if it has landed, otherwise decide it here.

## Acceptance criteria

- [ ] The easy-handle inputs replay against both libraries and one run's report is recorded under Notes, each new difference filed.
- [ ] Projects build clean with -warnaserror and their fast tests pass on Windows; every test is platform-neutral (Linux and macOS CI).
- [ ] Measure-CodeQuality.ps1 reports 0 failing members for Curl.Conformance.Differential.UnitLibrary.

## Notes

- Split out of BL-3217 by its lane-3 run on 2026-10-22.

## Log

- 2026-10-22: Created.
- 2026-10-23: Backlog -> Doing. Criteria at claim: cd228cb8 ae951636 5e81fa96.
