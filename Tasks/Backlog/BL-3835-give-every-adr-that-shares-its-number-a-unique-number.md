---
id: BL-3835
title: Give every ADR that shares its number a unique number
priority: Normal
assignee: Claude
pipeline: docs
depends-on: [BL-3834]
touches: [Documentation/Planning/Decisions]
requirement: none
created: 2026-10-26
completed:
---
# BL-3835 — Give every ADR that shares its number a unique number

## Goal

Every ADR in `Documentation/Planning/Decisions` has a number no other ADR uses.

## Context

On 2026-10-26, 14 ADR numbers were shared on `work/dark-factory` (ADR-0783, 0785, 0786, 0791, 0793, 0794, 0796, 0801, 0802, 0803, 0804, 0815, 0816, 0817; list them with `git ls-files Documentation/Planning/Decisions | grep -oE 'ADR-[0-9]+' | sort | uniq -d`). Lanes numbered them independently. BL-3834 stops new clashes; this task fixes the existing ones. Waits on BL-3834 so no new clash lands while it runs.

For each shared number, the ADR committed first (by `git log --diff-filter=A`) keeps it; each other one gets the next free number. Rename the file, change its `# ADR-####` heading, and rewrite references to it everywhere (tasks in Backlog, Doing, Blocked and Deferred, docs, code comments, other ADRs), telling apart which of the clashing ADRs each reference means from its title or the commit that added both. Never edit archived tasks under `Tasks/Done/<date>/` (task-board rule); a reference there keeps the old number.

## Acceptance criteria

- [ ] `git ls-files Documentation/Planning/Decisions | grep -oE 'ADR-[0-9]+' | sort | uniq -d` prints nothing.
- [ ] Every renumbered ADR's heading matches its file name, and Notes list each old and new number with its title.
- [ ] `grep` for each moved number outside `Tasks/Done` finds only references to the ADR that kept it.
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

## Log

- 2026-10-26: Created.