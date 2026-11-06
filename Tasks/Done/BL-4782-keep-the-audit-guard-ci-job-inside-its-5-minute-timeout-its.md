---
id: BL-4782
title: Keep the audit-guard CI job inside its 5-minute timeout: its full-history checkout ran past it on runs 41688300433 and 41723085402 and turned CI red
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [.github/workflows/ci.yml]
requirement: none
created: 2026-11-03
completed: 2026-11-06
---
# BL-4782 — Keep the audit-guard CI job inside its 5-minute timeout: its full-history checkout ran past it on runs 41688300433 and 41723085402 and turned CI red

## Goal

The `audit-guard` job (`Audit paths untouched by the dark factory`, .github/workflows/ci.yml) finishes inside its `timeout-minutes` on every `CI` run on work/dark-factory, so a slow clone no longer turns CI red when no test has failed.

## Context

Found while measuring BL-4647 and its sibling tasks (AF-0541 and others) over the shifts of 2026-11-03. Runs 41688300433 (28cbd2df, ended 18:20:55Z) and 41723085402 (441bcff2, ended 22:20:56Z) failed only because `audit-guard` hit its 5-minute `timeout-minutes` ("The job has exceeded the maximum execution time of 5m0s") inside `actions/checkout@v4` with `fetch-depth: 0`. The job log ends with checkout's post step killing orphan `git` and `git-remote-https` processes, so the full-history clone (21,000+ files, a long history) ran past 5 minutes. On other runs the same job took 25 seconds to 4.5 minutes. These two runs started red spells of 122.97 and (with 41715208450) 71.8 minutes, measured by `Audit/Tools/Measure-FactoryProcess.ps1`.

ci.yml is a guard file (ADR-0267, BL-1209), so it changes only through the `audit` branch, in an interactive session. Options: a shallower fetch (the guard needs only the merge base with origin/master), a blobless partial clone (`filter: blob:none`), or a longer `timeout-minutes`.

## Acceptance criteria

- [x] `audit-guard` in .github/workflows/ci.yml is changed on the `audit` branch so its checkout and check fit inside its `timeout-minutes`, and `Audit/Guard/Test-AuditPathsUntouched.ps1 -SelfTest` still passes.
- [x] The `CI` runs on work/dark-factory over the first full shift after the change merges show no `audit-guard` job cancelled by its `timeout-minutes` (`gh run view <id> --json jobs`).

## Notes

Measured 2026-11-06 (interactive): the 8 CI runs on work/dark-factory during shift 20261106-051223 (05:12-13:12) all have 'Audit paths untouched by the dark factory' success; none was cancelled by its timeout-minutes.

Criterion 1 met on 2026-11-03: PR #294 (audit -> master, merge 63cd76682) gives audit-guard a blobless checkout (`filter: blob:none`) and `timeout-minutes: 15`; `Audit/Guard/Test-AuditPathsUntouched.ps1 -SelfTest` passes (exit 0). Criterion 2 waits for the first full shift after the merge.

## Log

- 2026-11-03: Created.
- 2026-11-06: Backlog -> Doing. Criteria at claim: 0432a82d 58bf26f6.
- 2026-11-06: Doing -> Done. Over a full shift, no audit-guard job hit its timeout (8 of 8 succeeded).
