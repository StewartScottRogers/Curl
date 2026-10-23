---
id: BL-3422
title: Diagnose CliHttpDifferentialTests and LibcurlEasyDifferentialTests hanging past 150 s with the reference curl 8.22.0 set
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-3405]
touches: [Curl.Conformance.Differential.IntegrationTests/Cases/CliHttp, Curl.Conformance.Differential.IntegrationTests/LibcurlEasy]
requirement: none
created: 2026-10-23
completed: 2026-10-23
---
# BL-3422 — Diagnose CliHttpDifferentialTests and LibcurlEasyDifferentialTests hanging past 150 s with the reference curl 8.22.0 set

## Goal

<!-- One sentence: the observable outcome once this task is done. -->

## Context

<!-- Why this matters, and where to start: requirement IDs, ADRs, projects, files, upstream curl links with the curl version. -->

## Acceptance criteria

- [x] <!-- A statement someone else can check from the repository without asking a question. -->

## Notes

- Diagnosis: neither test hangs; both are slow. CliHttp replays 885 cases (454 + 431) and LibcurlEasy a similar volume, each through two binaries; our curl.exe (Debug) costs about 1.2 s per start against 0.03 s for the reference. Run alone with the reference set (Set-DifferentialEnvironment.ps1), CliHttpDifferentialTests passed in 10 m 57 s and LibcurlEasyDifferentialTests in 10 m 28 s, inside their 3 600 000 ms test timeouts. BL-3405 killed them at its 150 s cap per class, so run them with a cap of 20 minutes or more. No code changed.

## Log

- 2026-10-23: Created.
- 2026-10-23: Backlog -> Doing. Criteria at claim: fbecace0.
- 2026-10-23: Doing -> Done. CliHttp and LibcurlEasy differential tests are slow, not hung: both pass in about 11 minutes with the reference set
