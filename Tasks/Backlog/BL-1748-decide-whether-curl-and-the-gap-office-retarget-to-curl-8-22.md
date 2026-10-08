---
id: BL-1748
title: Decide whether Curl and the gap office retarget to curl 8.22.0
priority: Low
assignee: Claude
pipeline: docs
depends-on: [BL-1747]
touches: [Documentation/Planning/Decisions/README.md]
lane: no
requirement: none
created: 2026-10-08
completed:
---
# BL-1748 — Decide whether Curl and the gap office retarget to curl 8.22.0

## Goal

An ADR, "Decided by Claude under Stewart's delegation", says whether Curl's targeted curl
version moves from 8.21.0 to 8.22.0 now, and why. When the answer is yes, the tasks that
carry out the move are filed.

## Context

This is ADR-0433 decision 6: targeting a newer version is a recorded decision. curl 8.22.0
was released before the gap office existed. The GitHub API's latest release on 2026-10-08
was `curl-8_22_0`. BL-1747's run and the release watcher will have recorded its
`scope: newest` gaps and the dashboard's score against the newest version.

Interactive only (`lane: no`): deciding needs the gap findings and dashboard data under
`Gap/`, which lanes may not read once BL-1746 is done.

Weigh these, using the measurements, not memory:

- how many target-scope gaps are still open against 8.21.0, against how many newest-scope
  gaps 8.22.0 adds;
- whether a matched 8.22.0 reference build exists on each platform. On Windows that means
  Git for Windows' mingw64 `curl.exe`, so check which version the current Git for Windows
  ships. On Linux and macOS it means the distribution or Homebrew OpenSSL build;
- what moving costs: re-vendoring `Curl.Conformance.UnitTests/UpstreamTestData` with its
  `Update-UpstreamTestData.ps1 -Tag curl-8_22_0`; `UpstreamCaseRunner.CurlVersion`; the
  "8.21.0" strings Curl prints (for example in `-V` and the User-Agent); the reference
  fixtures measured on 8.21.0 throughout the tests and ADRs; and
  `Gap/Baselines/target.json`.

When the answer is yes, the ADR lists the follow-up tasks, and this task files them with
`task-board.ps1 new`. The `Gap/Baselines/target.json` change and the flip of
`scope: newest` findings to `scope: target` are interactive only, because they write `Gap/`.
The Curl-side changes are lane-eligible. When the answer is no, the ADR names the condition
that would change it, for example a matched reference build becoming available.

## Acceptance criteria

- [ ] A new ADR under `Documentation/Planning/Decisions/` records the decision, marked "Decided by Claude under Stewart's delegation", citing ADR-0433 and the measured counts.
- [ ] `Documentation/Planning/Decisions/README.md`'s index lists it.
- [ ] When the decision is to retarget, every follow-up task the ADR names exists on the board, with the `Gap/` ones marked `lane: no`. When it is not, the ADR states the condition for revisiting.

## Notes

## Log

- 2026-10-08: Created.
