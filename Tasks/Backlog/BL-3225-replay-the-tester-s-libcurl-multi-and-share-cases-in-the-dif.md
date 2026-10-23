---
id: BL-3225
title: Replay the tester's Libcurl multi and share cases in the differential runner
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-3224]
touches: [Curl.Conformance.Differential.UnitLibrary/Libcurl/Multi, Curl.Conformance.Differential.UnitTests/Libcurl/Multi, Curl.Conformance.Differential.IntegrationTests/LibcurlMulti]
requirement: none
created: 2026-10-22
completed:
---
# BL-3225 — Replay the tester's Libcurl multi and share cases in the differential runner

## Goal

The differential runner replays the tester's Libcurl multi and share cases (`m_in.json`, `m2.json`, `m2i.json`, `m3.json`, `mi.json`, `sh.json`) against Curl's Libcurl and libcurl 8.22.0.

## Context

The tester's harness and data are in `Z:\repos\Curl.difftest\2026-10-22\` (outside the repo; its Python scripts are a reference only - never commit or run Python). Parent task: BL-3217 (Stewart's 2026-10-22 prioritised findings list, "CI differential harness"); the runner's ADR comes from BL-3218. Drivers: `LibTest/MultiMode.cs`, `LibTest/ShareMode.cs`; outputs `m_out.txt`, `m2o.txt`, `m3o.txt`, `mo.txt`, `sho.txt`.

## Acceptance criteria

- [ ] The multi and share inputs replay against both libraries and one run's report is recorded under Notes, each new difference filed.
- [ ] Projects build clean with -warnaserror and their fast tests pass on Windows; every test is platform-neutral (Linux and macOS CI).
- [ ] Measure-CodeQuality.ps1 reports 0 failing members for Curl.Conformance.Differential.UnitLibrary.

## Notes

- Split out of BL-3217 by its lane-3 run on 2026-10-22.

## Log

- 2026-10-22: Created.
