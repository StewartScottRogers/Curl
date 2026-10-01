---
id: BL-1015
title: Write the planted-defect catalogue and the audit-seeder agent that plants them
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-1001]
touches: [.claude/agents/audit-seeder.md, Audit/PlantedDefects]
lane: no
requirement: none
created: 2026-09-29
completed:
---
# BL-1015 — Write the planted-defect catalogue and the audit-seeder agent that plants them

## Goal

Before an audit, the `audit-seeder` agent plants 6 to 10 known defects from a catalogue - at least one for each of the six auditors - into a throwaway detached worktree, and writes a manifest of what it planted where the auditors cannot see it, so each auditor's catch rate can be measured.

## Context

Interactive only (`lane: no`): it writes `.claude/agents/audit-*` and `Audit/`. Run it
with `/task-run BL-1015`. Design: the ADR from BL-994 (5 to 10 planted defects; every
auditor must have at least one to have a catch rate, so 6 to 10 here; Sonnet).

Catalogue - `Audit/PlantedDefects/Catalogue.md` plus `README.md`. At least 3 entries per
auditor (18 or more), each: `id` (`PD-###`), `auditor`, `kind`, a description of the
defect, how to plant it in general terms (a pattern to find and a change to make, so it
still applies after the code moves on), and how a catch is recognised (the file, and
the words or `key` fragment a correct finding would contain). Examples of the intent:
quality - weaken a test's only meaningful assertion to `Assert.IsNotNull`, rename a test
so its name promises what it does not check; security - replace a
`CryptographicOperations.FixedTimeEquals` with `SequenceEqual`, write a password into a
verbose line; performance - add a per-byte allocation in a body-copy loop, a 200 ms
delay on the `--version` path; conformance - change the exit code of one failure path,
change the casing of one verbose line; truthfulness - rename a method so its name
contradicts its behaviour, change one number in an ADR's statement that the code still
contradicts; process - a synthetic log excerpt added to the copied log folder showing a
task claimed four times, or a 90-minute CI red spell. Process defects are planted in a
copy of the log folder (`<audit dir>\logs`), never in the real `<repo>.logs`.

Agent - `.claude/agents/audit-seeder.md`: `name: audit-seeder`, `tools: Read, Grep,
Glob, Bash, Edit, Write` (it must change files, but only inside the worktree and log copy
it is given), `model: sonnet`. Its prompt input: the planted worktree path (a detached
worktree `RunAudit.ps1` creates at the audited commit), the log-copy path, the manifest
path (outside the worktree, under `<repo>.audit\<stamp>\`), a seed, and the count.
It must:

1. choose the defects with the seed, at least one per auditor;
2. plant each so the solution still builds (`dotnet build -warnaserror`, except where
   the defect is a build-visible one such as an AOT warning, which the catalogue says);
3. delete `Audit/PlantedDefects/`, `Audit/Findings/` and `Audit/Scorecards/` from the
   planted worktree (so an auditor that looks cannot see the answer or old findings),
   and commit everything in that worktree as one commit with the message `Audit baseline`
   - no branch name, never pushed;
4. write the manifest JSON: `{ seed, commit, planted: [ { id, auditor, file, line,
   description, catch } ] }`;
5. never touch the checkout it was started in.

## Acceptance criteria

- [ ] `Audit/PlantedDefects/Catalogue.md` has at least 3 entries per auditor, each with the fields above, and `README.md` explains the catalogue and says auditors must never read this folder.
- [ ] `.claude/agents/audit-seeder.md` exists with `name: audit-seeder`, `model: sonnet` and the tools above, and states the five duties.
- [ ] `claude agents` lists `audit-seeder`.
- [ ] A trial run on a detached worktree of `HEAD` with count 6 and seed 1 leaves that worktree with one new commit `Audit baseline`, no `Audit/PlantedDefects` folder, a building solution, and a manifest listing 6 defects covering all six auditors; the checkout it was started in is unchanged. Command and manifest summary under Notes; the trial worktree is removed afterwards.

## Notes

## Log

- 2026-09-29: Created.
- 2026-09-30: Backlog -> Doing.
