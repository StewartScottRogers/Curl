---
id: BL-1286
title: Re-run AF-0025's reproduction after BL-1281 and have the process measurer count only pushed claims
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-1281]
touches: [RunDarkFactory.ps1]
lane: no
requirement: none
created: 2026-10-02
completed:
---
# BL-1286 — Re-run AF-0025's reproduction after BL-1281 and have the process measurer count only pushed claims

## Goal

AF-0025's reproduction (in BL-1281's Context) counts only real, pushed claims, and run with a `-Since` after BL-1281 reached `work/dark-factory` it shows no task claimed 3 or more times.

## Context

Interactive only: the reproduction runs the audit office's process measurer, which no dark factory lane may read or run (ADR-0267), so BL-1281's lane could not check it. Its `touches` name only `RunDarkFactory.ps1` because a lane may not even file a task naming the measurer's path; an interactive session adds that path when it starts.

What BL-1281 found from `Curl.logs` and `git log`:

- BL-1121 was claimed once: one `claim` line (`DarkFactory-20261001-114139-L1.log`) and one `chore(tasks): claim BL-1121` commit. The measurer's 5 is a counting artefact; the other lanes' run transcripts (`BL-1113`, `BL-1116`, `BL-1122`, `BL-1124`, `BL-1134` `.jsonl` of shift 20261001-114139) mention "claim BL-1121" in git output, which is the likeliest source.
- BL-907: one real claim plus two `claim   lost the race; picking again` lines from lane 2, written when its pushes failed because GitHub was out of reach (2026-09-30 21:40-21:45).
- BL-892: two real claims. Lane 2 finished it, then three fetches failed with "unable to access" in about a minute and the lane parked the finished work; lane 3 did it again. The two `lost the race` lines in lane 3's log add the other 2.

BL-1281 changed `RunDarkFactory.ps1`: a failed claim push is traced as `race` (and not at all when origin is out of reach), only a pushed claim is traced `claim`, and claims and integrations wait up to an hour for origin to answer instead of parking.

## Acceptance criteria

- [ ] The measurer counts a task's claims from `claim` trace lines only (or `chore(tasks): claim` commits), not from mentions in other tasks' transcripts nor from `race` lines.
- [ ] AF-0025's reproduction, run with `-Since` set to a date after BL-1281 reached `work/dark-factory`, shows no task claimed 3 or more times.

## Notes

## Log

- 2026-10-02: Created.
