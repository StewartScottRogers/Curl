---
id: BL-4779
title: Let a lane's claim go before ordinary integrations waiting for the integrate lock
priority: High
assignee: Claude
pipeline: direct
depends-on: []
touches: [DarkFactory/Claim.ps1, DarkFactory/Integrate.ps1]
lane: no
requirement: none
created: 2026-11-03
completed: 2026-11-03
---
# BL-4779 — Let a lane's claim go before ordinary integrations waiting for the integrate lock

## Goal

A lane waiting to claim a task takes the integrate lock ahead of lanes waiting to integrate ordinary work, so idle lanes start work while integrations queue.

## Context

Stewart, 2026-11-03: "execute as many tasks in parallel as you can". At 14:02 the shift started 13:46 had 13 lanes and a board capacity of 20, but 7 lanes sat in `claim` for 15 minutes: `Invoke-Claim` (DarkFactory/Claim.ps1) takes `Enter-Lock` without `-Priority`, so each claim waited behind every ordinary integration's build and fast tests, while the audit-fix tasks themselves took about 1.5 minutes. A claim holds the lock for seconds; ADR-1158 already lets a CI fix's integration go first through the same priority marker.

## Acceptance criteria

- [x] `Invoke-Claim` in `DarkFactory/Claim.ps1` takes the lock with `Enter-Lock -Priority`.
- [x] `RunDarkFactory.ps1 -TestIntegrateLock` has a check that the claim takes the lock with `-Priority`, and passes under Windows PowerShell 5.1 and pwsh 7.

## Notes

## Log

- 2026-11-03: Created.
- 2026-11-03: Backlog -> Doing. Criteria at claim: 51ce1724 e0a484c3.
- 2026-11-03: Doing -> Done. Invoke-Claim takes Enter-Lock -Priority; -TestIntegrateLock checks it and passes under Windows PowerShell 5.1 and pwsh 7 (24 pass each).
