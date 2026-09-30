---
id: BL-1019
title: Report when an audit is due
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-1001]
touches: [Audit/Tools/Test-AuditDue.ps1]
lane: no
requirement: none
created: 2026-09-29
completed:
---
# BL-1019 — Report when an audit is due

## Goal

`Audit/Tools/Test-AuditDue.ps1` says whether an audit is due and why - no audit yet, `RunDarkFactory.ps1` changed since the last audited commit, or a roadmap milestone was completed since then - so the shift's end (BL-1022) and interactive sessions can act on the cadence.

## Context

Interactive only (`lane: no`): it writes `Audit/`. Run it with `/task-run BL-1019`.
Cadence from the ADR recorded by BL-994: audits run on demand, before each
roadmap-milestone merge to `master`, and after changes to `RunDarkFactory.ps1`.
Scorecards (BL-1001) record the audited commit in their header.

Design:

- `-Ref` (default `origin/work/dark-factory`), `-ScorecardsRef` (default
  `origin/master`: scorecards reach `master` through the audit pull request; read them
  with `git ls-tree`/`git show`, so the script works from any checkout, including one
  whose `Audit/` is older).
- Reasons, each on its own line:
  - `no-audit`: no scorecard exists at `-ScorecardsRef`;
  - `factory-script`: `git diff --name-only <last audited commit> <Ref> --
    RunDarkFactory.ps1` is non-empty;
  - `milestone:<N>`: in `Documentation/Planning/Roadmap.md`, a `- **Status:**` line
    under `## Milestone <N>` does not start with `Done` at the last audited commit and
    does at `<Ref>`.
- Output: first line `Audit due: <reason>[, <reason>]` or
  `No audit due: last audit <scorecard name> covers <commit>`. `-Json` prints
  `{ due, reasons: [..], lastScorecard, lastCommit }`. Exit code 0 either way (a due
  audit is information, not an error); exit 2 only when git fails.
- Reads git only; never fetches (callers fetch first) and never writes.

`-SelfTest` builds a scratch repository with a scorecard, a later change to
`RunDarkFactory.ps1`, and a Roadmap milestone moving from `In progress` to `Done`, and
checks each reason alone and together.

## Acceptance criteria

- [ ] `-SelfTest` prints `PASS` and no `FAIL` for: no scorecard gives `no-audit`; a `RunDarkFactory.ps1` change gives `factory-script`; a milestone moving to `Done` gives `milestone:<N>`; both at once give both; neither gives `No audit due`.
- [ ] Run in this repository today it prints `Audit due: no-audit` (no scorecard on `master` yet), exit 0.
- [ ] `-Json` output parses with `ConvertFrom-Json` and has the four fields.
- [ ] Header help documents reasons, parameters and exit codes; ASCII only; runs under PowerShell 7 and Windows PowerShell 5.1.

## Notes

## Log

- 2026-09-29: Created.
