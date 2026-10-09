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
completed:
---
# BL-1840 — Run the gap cross-check's upstream cases in a scratch folder, never the main checkout

## Goal

The gap cross-check runs every upstream case in its own temporary folder, so no case can write a file into the repository checkout.

## Context

- 2026-10-08: the first gap run's cross-check (Measure-ReferenceCrossCheck.ps1) left a file named `%` (a canned HTTP reply) in Z:\repos\Curl. That dirty tree made the next dark factory shift refuse to start (working tree not clean).
- Run each case (reference curl and Curl) with its working directory set to a fresh folder under the run's scratch directory, deleted afterwards. `-o` and log-dir placeholders resolve there too.
- Interactive only (`Gap/` is an audit path).

## Acceptance criteria

- [ ] After a cross-check run, `git status` in the checkout it was started from shows nothing new; a self-test checks that a case writing `-o %` lands in the scratch folder.
- [ ] Merged to master through a green audit pull request.

## Notes

## Log

- 2026-10-08: Created.
- 2026-10-08: Backlog -> Doing.
