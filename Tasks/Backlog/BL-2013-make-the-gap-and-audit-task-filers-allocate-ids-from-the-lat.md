---
id: BL-2013
title: Make the gap and audit task filers allocate IDs from the latest board and dedupe before pushing
priority: High
assignee: Claude
pipeline: direct
depends-on: []
touches: [Gap/Tools, Audit/Tools]
lane: no
requirement: none
created: 2026-10-10
completed:
---
# BL-2013 — Make the gap and audit task filers allocate IDs from the latest board and dedupe before pushing

## Goal

When the audit office and the gap office file tasks at about the same time, no two live tasks share an ID.

## Context

On 2026-10-10 the Audit tab released AF-0115 and AF-0141..AF-0152 as BL-1959..BL-1971 at 07:08. The gap run started at 06:57 and filed its own BL-1959..BL-1971 at 07:58, from the board as it stood when it started. `task-board.ps1 move` refuses an ID that names two live tasks, so all 9 lanes of the 08:00 shift claimed nothing for 80 minutes ("claim kept losing races"). It was fixed by hand: `dedupe -Since 7f0e9695b` renumbered the gap's tasks to BL-1997..BL-2009 (702cd24ed), and PR #104 repointed GF-0001..GF-0016. Start from the gap run's task filing in Gap/Tools and from Audit/Tools/New-TasksFromAcceptedFindings.ps1 and the Audit tab's release path.

## Acceptance criteria

- [ ] Each filer fetches origin/work/dark-factory right before it allocates IDs. If its push is refused, it re-fetches, runs `task-board.ps1 dedupe -Since <the ref it started from>`, repoints its findings' `task:`/`tasks:` fields to the new IDs and pushes again.
- [ ] A test or a `-WhatIf` dry run shows two filers started from the same board end up with distinct IDs, and each finding's task links name its own tasks.

## Notes

## Log

- 2026-10-10: Created.
