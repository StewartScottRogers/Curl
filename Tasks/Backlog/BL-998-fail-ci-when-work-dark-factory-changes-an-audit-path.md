---
id: BL-998
title: Fail CI when work/dark-factory changes an audit path
priority: High
assignee: Claude
pipeline: direct
depends-on: [BL-993, BL-994]
touches: [.github/workflows/ci.yml, Audit/Guard]
lane: no
requirement: none
created: 2026-09-29
completed:
---
# BL-998 — Fail CI when work/dark-factory changes an audit path

## Goal

The `CI` workflow fails, with one line per offending path, whenever the head of `work/dark-factory` (pushed, or as a pull request) carries a change of its own to `Audit/` or `.claude/agents/audit-*`; changes that reached it from `master` never fail it.

## Context

Interactive only (`lane: no`): audit guard layer 3 (the ADR from BL-994). Run it with
`/task-run BL-998`.

Audit changes reach `master` only through the `audit` branch's pull request. The factory
branch may later receive them by merging `master`, or by being cut from it; neither may
fail CI. A red `CI` run blocks the shift-end merge already (`Invoke-MergeToMaster` in
`RunDarkFactory.ps1` requires the run's conclusion `success`), so any failing job is
enough. `.github/workflows/ci.yml` today has one job, `test`, a Windows/Linux/macOS
matrix; it runs on every push except `gource`, `board` and `factory/**`, and on pull
requests.

The rule, precisely. With `B = origin/master` and `H` = the commit under test (the pushed
commit, or `github.event.pull_request.head.sha` for a pull request - not the synthetic
merge commit):

- `mine` = `git diff --name-only B...H` (three dots: what `H` changed since the merge
  base, so a change `master` made and `H` merged in is excluded, because merging moves
  the merge base past it);
- `differs` = `git diff --name-only B H` (two dots: paths whose content differs now);
- offending = paths in both lists that match `^Audit/` or `^\.claude/agents/audit-`.
  Requiring `differs` passes a path the factory changed to exactly what `master` has
  (e.g. a cherry-picked audit commit); requiring `mine` passes a path only `master`
  changed.

Put the logic in `Audit/Guard/Test-AuditPathsUntouched.ps1` (params `-Base`, `-Head`;
prints `Audit guard: <path> changed on work/dark-factory since its merge base with
master` per offending path, plus a GitHub `::error::` annotation, and exits 1 if any,
else prints `Audit guard: no audit path changed` and exits 0). Because it lives under
`Audit/`, a change to it on the factory branch is itself an offending path. The CI step
runs master's copy: `git show origin/master:Audit/Guard/Test-AuditPathsUntouched.ps1`
into a temporary file, falling back to the checked-out copy only while `master` has
none.

In `ci.yml` add a job `audit-guard` ("Audit paths untouched by the dark factory"),
`runs-on: ubuntu-latest`, `if: github.ref == 'refs/heads/work/dark-factory' ||
github.head_ref == 'work/dark-factory'`, `actions/checkout@v4` with `fetch-depth: 0`,
`git fetch origin master`, then the script under `shell: pwsh`. The `audit` branch and
every other branch skip the job.

The script also takes `-SelfTest`: it builds a scratch git repository in a temporary
folder and checks the cases below, printing `PASS`/`FAIL` per case.

## Acceptance criteria

- [ ] `pwsh -File Audit/Guard/Test-AuditPathsUntouched.ps1 -SelfTest` prints `PASS` and no `FAIL` for: (a) a factory commit adding `Audit/Findings/x.md` fails; (b) a factory commit changing `.claude/agents/audit-quality.md` fails; (c) a change to `Audit/x.md` made only on master passes; (d) that master change merged into the factory branch passes; (e) the same change cherry-picked onto the factory branch passes; (f) a factory commit changing `Curl.Core.UnitLibrary/x.cs` and `.claude/agents/code-reviewer.md` passes; (g) a factory commit changing `Audit/Guard/Test-AuditPathsUntouched.ps1` fails.
- [ ] The self-test also passes under Windows PowerShell 5.1 (`powershell -NoProfile -File ...`), and the script is ASCII only.
- [ ] `.github/workflows/ci.yml` has the `audit-guard` job with the `if:` condition, `fetch-depth: 0`, and the run of master's copy with the fallback; the `test` job is unchanged.
- [ ] `ci.yml` is valid YAML (the `CI` workflow's next run on this branch shows the new job, or skips it by its condition, without a workflow syntax error) - recorded under Notes with the run link.
- [ ] A comment at the top of the job says what it guards and cites the ADR from BL-994.

## Notes

## Log

- 2026-09-29: Created.
