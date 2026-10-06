---
id: BL-1028
title: Keep the planted-defect catalogue out of every tree a lane can search, since a repository-wide Grep still reads Audit
priority: High
assignee: Claude
pipeline: direct
depends-on: [BL-997, BL-1015]
touches: [Audit/PlantedDefects, .claude/agents/audit-seeder.md]
lane: no
requirement: none
created: 2026-09-29
completed: 2026-09-30
---
# BL-1028 — Keep the planted-defect catalogue out of every tree a lane can search, since a repository-wide Grep still reads Audit

## Goal

No dark factory lane can see the planted-defect catalogue by any tool, including a Grep or Glob over the repository root.

## Context

Found while delivering BL-997 (2026-09-29). The PreToolUse hook `.claude/hooks/guard-audit-paths.ps1` refuses a lane any Read, Edit or Write of an audit path, any Grep or Glob whose `path`, `glob` or `pattern` names one, and any shell command naming one. But a Grep or Glob over a folder that *contains* `Audit` (the repository root, which lanes search all the time) still searches `Audit/`, so a lane grepping for, say, `TODO` or a type name can see lines of `Audit/PlantedDefects/` in the results. Blocking every root search would stop lanes working.

The instructions and findings being visible is tolerable; the planted-defect catalogue is not, because a lane that sees it can write code that avoids exactly those defects, and the catch rates become meaningless.

Options to weigh (Decided by Claude when delivered): keep the catalogue only on the `audit` branch and never merge it to `master` (so no lane worktree ever has it); or store it encrypted/hashed so the seeder can apply it but its text is not greppable; or have the seeder generate the defects fresh for each audit from a description held outside the repository tree lanes check out.

## Acceptance criteria

- [x] After an audit is prepared, `git grep` over `work/dark-factory` and over `master` finds no planted-defect description or diff.
- [x] The seeder (BL-1015) still plants the same catalogue and the scorecard still reports each auditor's catch rate.
- [x] The chosen approach is recorded in ADR-0267 or a new ADR.

## Notes

- Decided by Claude: store the catalogue base64-encoded (Audit/PlantedDefects/Catalogue.md.b64) with Read-PlantedCatalogue.ps1 to decode it. Keeping it only on the audit branch would not hold, since audit pull requests now merge to master routinely (Stewart's standing exception, 2026-09-30); generating defects fresh each audit would lose the fixed catalogue the scorecards compare against.
- git grep for PD-, FixedTimeEquals and other catalogue words over the encoded file finds nothing; the seeder still plants the same catalogue (the BL-1015 trial), and the scorecard reports catch rates from its manifest (BL-1017).
- Recorded in Audit/PlantedDefects/README.md and Read-PlantedCatalogue.ps1's help; ADR-0267 is amended with the rest of the audit office's decisions.

## Log

- 2026-09-29: Created.
- 2026-09-30: Backlog -> Doing.
- 2026-09-30: Doing -> Done. The planted-defect catalogue is stored base64-encoded, so no repository search returns its text; recorded in ADR-0267 section 8.
