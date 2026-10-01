---
id: BL-1020
title: Run an audit end to end with Audit\RunAudit.cmd
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-1009, BL-1010, BL-1011, BL-1012, BL-1013, BL-1014, BL-1015, BL-1016, BL-1017, BL-1019]
touches: [Audit/RunAudit.ps1, Audit/RunAudit.cmd]
lane: no
requirement: none
created: 2026-09-29
completed:
---
# BL-1020 — Run an audit end to end with Audit\RunAudit.cmd

## Goal

`Audit\RunAudit.cmd -NewTab` starts an audit in its own herdr tab (or console window outside herdr): it plants defects in a throwaway worktree of the factory's branch, runs the six auditors against it, writes findings and a scorecard on the `audit` branch, pushes that branch and opens (never merges) a pull request to `master` for Stewart.

## Context

Interactive only (`lane: no`): it writes `Audit/`. Run it with `/task-run BL-1020`.
Design: the ADR from BL-994. It lives under `Audit/` (not at the repository root) so the
guards protect the runner too. PowerShell only.

Pieces it drives: agents `audit-seeder` (BL-1015) and `audit-quality`, `-security`,
`-performance`, `-conformance`, `-truthfulness`, `-process` (BL-1009 to BL-1014);
`Write-AuditFindings.ps1` (BL-1016); `Write-AuditScorecard.ps1` (BL-1017);
`Get-AuditorFingerprint.ps1` (BL-1002); `Test-AuditDue.ps1` (BL-1019). Headless agent
runs: `claude -p --agent <name> --model <model> --dangerously-skip-permissions
--output-format stream-json --verbose`, prompt on standard input, as
`RunDarkFactory.ps1`'s `Invoke-TaskRun` does; the `result` event gives cost and tokens.

Herdr and `-NewTab`: copy the small pieces of `RunDarkFactory.ps1` that open a tab
(`Get-HerdrBin`, the `-NewTab` hand-off near line 656, tab captions) rather than
dot-sourcing that script, which runs on load. Caption: `Audit <HH:mm> · <phase>`.

Steps, each traced to `<repo>.audit\<stamp>\audit.log`:

1. Refuse when `CURL_DARK_FACTORY_LANE` is set, and while a dark factory shift runs (a
   process whose command line contains `RunDarkFactory.ps1` and not `-NewTab`/`-Restart`),
   with a message saying audits run between shifts.
2. `git fetch origin`. Worktree `<repo>.audit\audit-branch` on branch `audit` (from
   `origin/audit` if it exists, else from `origin/master`), then merge `origin/master`
   into it so its audit definitions are current.
3. Planted tree: `git worktree add --detach <repo>.audit\<stamp>\planted <Ref>` (`-Ref`,
   default `origin/work/dark-factory`). Overlay `Audit/Instructions`, `Audit/Tools` and
   `.claude/agents/audit-*.md` from the audit-branch worktree, so auditors run the
   definitions the fingerprint describes. Copy the factory logs changed since the last
   scorecard from `<repo>.logs` into `<stamp>\logs`.
4. Seeder with `-Planted` (default 8; at least one per selected auditor, at most 10) and
   `-Seed` (default: from the clock; recorded). Manifest to `<stamp>\manifest.json`.
5. Each selected auditor (`-Auditors`, default all six, in the fixed order), one at a
   time, working directory the planted tree, prompt naming the tree, a scratch folder
   `<stamp>\scratch\<auditor>`, the log copy (process), and that auditor's open findings
   from the audit branch (`proposed` or `accepted`) to re-audit. Reply to
   `<stamp>\reports\<auditor>.md`. Afterwards `git -C planted status --porcelain` must be
   empty; if not, record "wrote to the audited tree" for that auditor (it is then
   unreliable) and `git reset --hard` plus `git clean -fdx` before the next.
6. `Write-AuditScorecard.ps1 -ReliabilityOnly`, then `Write-AuditFindings.ps1 -Unreliable
   ...`, then `Write-AuditScorecard.ps1`, all writing into the audit-branch worktree.
7. Commit there (`audit: scorecard <stamp>` plus counts), push `origin audit`, and open a
   pull request `audit` -> `master` with `gh pr create` if none is open (the body: the
   scorecard's auditor table, links to new findings, and "Stewart approves each merge of
   this branch; there is no standing exception"). Never merge it.
8. Remove the planted worktree; keep `<stamp>` for reading. Whisper and caption the tab
   as finished, like the factory.

`-DryRun` performs step 1 and prints every command the other steps would run, changing
nothing.

## Acceptance criteria

- [ ] `Audit\RunAudit.cmd -DryRun` prints the planned steps with real paths and exits 0; with `CURL_DARK_FACTORY_LANE=1` set it refuses and exits non-zero.
- [ ] With a dark factory shift running, it refuses with the between-shifts message (checked by starting it while a shift runs, or by a `-SelfTest` case over a faked process list).
- [ ] A real run, `Audit\RunAudit.cmd -Auditors truthfulness,process -Planted 2`, ends with: a scorecard on `origin/audit` whose header carries the fingerprint `Get-AuditorFingerprint.ps1` prints on that branch; finding files for its non-planted findings; an open pull request `audit` -> `master` that is not merged; no `planted` worktree left in `git worktree list`; and the checkout it was started from unchanged. The run's folder and pull request link are recorded under Notes.
- [ ] `Audit\RunAudit.cmd -NewTab -DryRun` opens a herdr tab when run inside herdr (a console window otherwise) and returns at once.
- [ ] Header help documents parameters, steps and the refusals; ASCII only.

## Notes

## Log

- 2026-09-29: Created.
- 2026-09-30: Backlog -> Doing.
