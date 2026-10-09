---
id: BL-1840
title: Run the gap cross-check's upstream cases in a scratch folder, never the main checkout
priority: High
assignee: Claude
pipeline: direct
depends-on: []
touches: [Gap/Tools/Measure-ReferenceCrossCheck.ps1]
lane: no
requirement: none
created: 2026-10-08
completed: 2026-10-08
---
# BL-1840 — Run the gap cross-check's upstream cases in a scratch folder, never the main checkout

## Goal

The gap cross-check runs every upstream case in its own temporary folder, so no case can write a file into the repository checkout.

## Context

- 2026-10-08: the first gap run's cross-check (Measure-ReferenceCrossCheck.ps1) left a file named `%` (a canned HTTP reply) in Z:\repos\Curl. That dirty tree made the next dark factory shift refuse to start (working tree not clean).
- Run each case (reference curl and Curl) with its working directory set to a fresh folder under the run's scratch directory, deleted afterwards. `-o` and log-dir placeholders resolve there too.
- Interactive only (`Gap/` is an audit path).

## Acceptance criteria

- [x] After a cross-check run, `git status` in the checkout it was started from shows nothing new; a self-test checks that a case writing `-o %` lands in the scratch folder.
- [x] Merged to master through a green audit pull request.

## Notes

- 2026-10-08: Merged in PR #82. Cause: upstream test 1489 runs `-D %`. `Measure-ReferenceCrossCheck.ps1` used `Push-Location`, which does not change the process directory, and `Record-CurlExchange.ps1` starts curl with no `WorkingDirectory`, so the reference curl wrote `%` into Z:\repos\Curl. Each case now runs in its own scratch `%LOGDIR` with the process directory set and restored; the shared probe `Invoke-GapRun` uses a fresh temporary folder when given none. Self-tests cover both. An end-to-end cross-check of 413 cases left both checkouts clean.

- 2026-10-08: Merged in PR #82. Cause: upstream test 1489 runs `-D %`. `Measure-ReferenceCrossCheck.ps1` used `Push-Location`, which does not change the process directory, and `Record-CurlExchange.ps1` starts curl with no `WorkingDirectory`, so the reference curl wrote `%` into Z:\repos\Curl. Each case now runs in its own scratch `%LOGDIR` with the process directory set and restored; the shared probe `Invoke-GapRun` uses a fresh temporary folder when given none. Self-tests cover both. An end-to-end cross-check of 413 cases left both checkouts clean.

## Log

- 2026-10-08: Created.
- 2026-10-08: Backlog -> Doing.
- 2026-10-08: Doing -> Blocked. Being worked interactively on branch audit-bl-1840; parked so Doing holds nothing orphaned between shifts
- 2026-10-08: Blocked -> Doing.
- 2026-10-08: Doing -> Done. Merged in PR #82 with CI green on all three platforms
