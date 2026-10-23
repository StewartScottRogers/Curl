---
id: BL-3235
title: Replay the tester's CLI TLS cases in the differential runner
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-3233]
touches: [Curl.Conformance.Differential.UnitLibrary/Tls, Curl.Conformance.Differential.UnitTests/Tls, Curl.Conformance.Differential.UnitLibrary/Cases/CliTls, Curl.Conformance.Differential.IntegrationTests/CliTls]
requirement: none
created: 2026-10-22
completed:
---
# BL-3235 — Replay the tester's CLI TLS cases in the differential runner

## Goal

The differential runner replays the tester's CLI TLS cases (`cases_tls.py`, 70 cases) on a C# port of `tls_server.py` using the tester's `pki` certificates.

## Context

The tester's harness and data are in `Z:\repos\Curl.difftest\2026-10-22\` (outside the repo; its Python scripts are a reference only - never commit or run Python). Parent task: BL-3217 (Stewart's 2026-10-22 prioritised findings list, "CI differential harness"); the runner's ADR comes from BL-3231. Compare with `report_tls.txt`. Certificates are platform-sensitive: keep any unit test that loads one platform-neutral or pin each platform's answer.

## Acceptance criteria

- [ ] The TLS case file and server port exist, and one run's report is recorded under Notes with each new difference filed.
- [ ] Projects build clean with -warnaserror and their fast tests pass on Windows; every test is platform-neutral (Linux and macOS CI).
- [ ] Measure-CodeQuality.ps1 reports 0 failing members for Curl.Conformance.Differential.UnitLibrary.

## Notes

- Split out of BL-3217 by its lane-3 run on 2026-10-22.

## Log

- 2026-10-22: Created.
