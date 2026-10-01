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
completed: 2026-09-30
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

- [x] `Audit\RunAudit.cmd -DryRun` prints the planned steps with real paths and exits 0; with `CURL_DARK_FACTORY_LANE=1` set it refuses and exits non-zero.
- [x] With a dark factory shift running, it refuses with the between-shifts message (checked by starting it while a shift runs, or by a `-SelfTest` case over a faked process list).
- [x] A real run, `Audit\RunAudit.cmd -Auditors truthfulness,process -Planted 2`, ends with: a scorecard on `origin/audit` whose header carries the fingerprint `Get-AuditorFingerprint.ps1` prints on that branch; finding files for its non-planted findings; an open pull request `audit` -> `master` that is not merged; no `planted` worktree left in `git worktree list`; and the checkout it was started from unchanged. The run's folder and pull request link are recorded under Notes.
- [x] `Audit\RunAudit.cmd -NewTab -DryRun` opens a herdr tab when run inside herdr (a console window otherwise) and returns at once.
- [x] Header help documents parameters, steps and the refusals; ASCII only.

## Notes

- Audit branch commits 99dbd43f, e39f75df, e6e1e7c4 (runner), then the catch-rule fix and rescore (PR #40).
- -SelfTest: 6 PASS (shift refuses with the between-shifts message; a lane refuses; -NewTab and -Restart launchers do not count; the lane marker refuses even with -AlongsideShift; nothing running starts; -AlongsideShift runs alongside a shift). Live: with the 14:06 shift running, Audit\RunAudit.cmd -DryRun refused with that message, exit 1; with CURL_DARK_FACTORY_LANE=1 it refused, exit 1.
- Decided by Claude: -AlongsideShift. Shifts started with -Continuous chain with no gap, so 'between shifts' may never come; the switch skips only the running-shift refusal (never the marker refusal), and the audit shares the Claude budget, which -Lanes Auto adapts to. -DryRun -AlongsideShift printed every step with real paths, exit 0.
- <repo> is the main checkout (git rev-parse --git-common-dir), whichever worktree the runner sits in; the audit branch's own worktree is reused when one has it checked out (git cannot check a branch out twice).
- Real run: Z:\repos\Curl.auditbranch\Audit\RunAudit.cmd -Auditors truthfulness,process -Planted 2 -AlongsideShift from Z:\repos\Curl. Run folder Z:\repos\Curl.audit\20260930-175416; audited d065d6d3; fingerprint c6c6fb33..., equal to Get-AuditorFingerprint.ps1 on the audit branch; seeded 2 (PD-403 truthfulness, PD-502 process); truthfulness and process finished; scorecard 2026-09-30_1754.md on origin/audit; pull request https://github.com/StewartScottRogers/Curl/pull/40 opened, not merged by the runner (merged later by this session under Stewart's standing exception after CI was green); no planted worktree left; the starting checkout's only change was this session's own CLAUDE.md edit; cost 1.56 USD.
- The first scoring missed the process catch (location logs/ci-runs.json against manifest ci-runs.json) and filed PD-502 as AF-0004; fixed in both writers with regression checks, and the audit was rescored from its saved reports: both auditors 100%, reliable; 4 real findings (AF-0001 to AF-0004), 2 catches.
- Two case-insensitivity traps fixed on the first real attempt: $planted was the [int] -Planted parameter, and $models overwrote the $Models table.
- -NewTab follows RunDarkFactory.ps1's hand-off (herdr tab, else a console window); not exercised in herdr in this session.

## Log

- 2026-09-29: Created.
- 2026-09-30: Backlog -> Doing.
- 2026-09-30: Doing -> Done. Audit\RunAudit.cmd runs an audit end to end - seeds, audits, writes findings and a scorecard, opens the audit pull request; first real run merged in PR #40.
