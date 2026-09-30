---
id: BL-1004
title: Build a BCL-only mutation test runner that mutates one library at a time
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-1000]
touches: [Audit/Tools/Invoke-MutationTest.ps1]
lane: no
requirement: none
created: 2026-09-29
completed: 2026-09-30
---
# BL-1004 — Build a BCL-only mutation test runner that mutates one library at a time

## Goal

`Audit/Tools/Invoke-MutationTest.ps1 -Library Curl.<Area>.UnitLibrary` breaks that library's code on purpose, one small change at a time, in a throwaway worktree, runs its `.UnitTests` twin after each, and reports every mutant as killed, survived, timed out or stillborn, with the mutation score.

## Context

Interactive only (`lane: no`): it writes `Audit/`. Run it with `/task-run BL-1004`.
The quality auditor (BL-1009) uses it to find code whose tests would not notice a bug.
No mutation-testing package: the solution is base class library only, so this is
PowerShell over `git` and `dotnet`.

Design:

- Parameters: `-Library` (required; its twin is the same name with `.UnitTests`),
  `-Commit` (default `HEAD`), `-MaxMutants` (default 50), `-Seed` (default 0, for a
  reproducible sample), `-TimeoutSeconds` per test run (default 600), `-OutFile` (JSON).
- Never touch the checkout it is run from: `git worktree add --detach
  <repo>.audit\mutation-<stamp> <Commit>` beside the repository (like the factory's
  `<repo>.lanes`), and `git worktree remove --force` it at the end, even on failure.
- Build and test once unmutated first; if the baseline fails, stop and report that.
- Candidate sites: each line of each `.cs` file in the library (not `obj/`, not `bin/`),
  skipping comment lines, `using`/`namespace` lines, attributes, and text inside string
  or char literals. Operators: `==`->`!=`, `!=`->`==`, ` < `->` <= `, ` > `->` >= `,
  ` <= `->` < `, ` >= `->` > `, `&&`->`||`, `||`->`&&`, `+ 1`->`- 1`, `- 1`->`+ 1`,
  `true`->`false`, `false`->`true`, `!(`->`(`. The spaces around `<`/`>` keep generics
  out. Sample `-MaxMutants` sites with `System.Random($Seed)`.
- Per mutant: apply one change, `dotnet build <twin> -c Release -p:TreatWarningsAsErrors=false`
  (so an analyzer warning the mutant causes does not hide it; a failed build is **stillborn**, not counted), `dotnet test <twin> -c Release
  --no-build --filter "TestCategory!=Integration"`; failing tests = **killed**, passing =
  **survived**, over the timeout = **timed out** (counted as killed). Restore the file
  with `git checkout -- <file>` before the next.
- Output JSON: `{ library, commit, seed, baselineMs, mutants: [ { file, line, operator,
  original, mutated, outcome, ms } ], killed, survived, timedOut, stillborn, score }`
  where `score = (killed + timedOut) / (killed + timedOut + survived)`.
- `-SelfTest`: checks the site finder and operators on in-memory sample lines, including
  a string literal containing `==`, a `List<int>` generic, a `// a == b` comment, and a
  line with two operators (one mutant per operator occurrence).

## Acceptance criteria

- [x] `-SelfTest` prints `PASS` and no `FAIL` for the cases above.
- [x] `Invoke-MutationTest.ps1 -Library Curl.Protocol.Dict.UnitLibrary -MaxMutants 5 -Seed 1 -OutFile <tmp>.json` writes JSON with 5 mutants, each with an outcome, and a `score` between 0 and 1; running it twice gives the same five sites.
- [x] After that run, `git status --porcelain` in the checkout it was run from is unchanged and `git worktree list` shows no `mutation-` worktree.
- [x] A library whose baseline fails (simulate with `-Commit` of a commit that does not build, or a scratch break in the worktree via `-SelfTest`) stops with a message and no mutants.
- [x] Header help documents parameters, outcomes and the score formula; the script runs under PowerShell 7 and Windows PowerShell 5.1 and is ASCII only.

## Notes

- On the audit branch (worktree moved from Z:/repos/Curl.audit to Z:/repos/Curl.auditbranch, so <repo>.audit stays free for this tool's worktrees), commit 60dbb883, pull request https://github.com/StewartScottRogers/Curl/pull/31.
- -SelfTest: 17 PASS, 0 FAIL under Windows PowerShell 5.1 and PowerShell 7.6.6 (string, verbatim and escaped strings, char literal, List<int>, comment line, trailing comment, two operators, spaced comparisons, + 1 not + 10, true/false/!(, attribute and using lines, mutation applied, score, and a scratch project that does not compile stopping as a failed baseline).
- Curl.Protocol.Dict.UnitLibrary -MaxMutants 5 -Seed 1 -TimeoutSeconds 120, run twice: 35 sites; the same 5 each time - DictProtocolHandler.cs:115 > timedOut, DictRequest.cs:184 && and 185 && stillborn (|| leaves the out variable unassigned: a genuine compile error), DictRequest.cs:184 < killed, DictRequest.cs:149 || killed; score 1. git status in the checkout unchanged; no mutation- worktree left.
- Git calls go through Invoke-Git with ErrorActionPreference Continue: Windows PowerShell 5.1 turned git's stderr progress (Preparing worktree) into a terminating error.
- A mutant that loops forever costs the whole -TimeoutSeconds (default 600, as specified); the quality auditor (BL-1009) should pass a lower one, about 3x the baseline test time.

## Log

- 2026-09-29: Created.
- 2026-09-30: Backlog -> Doing.
- 2026-09-30: Doing -> Done. Invoke-MutationTest.ps1 mutation-tests one library reproducibly and reports the score; in PR #31, awaiting Stewart's merge.
