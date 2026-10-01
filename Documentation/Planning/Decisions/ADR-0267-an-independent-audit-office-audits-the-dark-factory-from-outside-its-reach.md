# ADR-0267 — An independent audit office audits the dark factory from outside its reach

- **Status:** Accepted
- **Date:** 2026-09-29

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-994.
The design was agreed with Stewart on 2026-09-29; this record states it and does not
reopen it. Tasks BL-995 to BL-1022 build it, and every audit task cites this ADR.

## Context

The dark factory (`RunDarkFactory.ps1`, ADR-0129) works the task board unattended and
merges its branch into `master` at the end of each shift. Its own gates - build, fast
tests, coverage, complexity, CI on three platforms - are gates the factory itself runs
and, in principle, can edit. On 2026-09-29 Stewart asked for an in-repository AI audit
office: specialist auditors that periodically audit the factory and the code it produced
for gaps in quality, security, performance and process.

The factory is the party audited. Anything it can edit it can weaken, even
unintentionally - a lane "fixing" a failing check by loosening the check - so the audit
office has to sit where no lane can reach it, and its results have to reach `master`
through someone other than the factory.

## Decision

### 1. Independence through four guard layers the factory cannot get past

The **audit paths** are exactly two:

- `Audit/` - a shared project, like `Tasks/` and `Documentation/`, holding the auditor
  instructions, the planted-defect catalogue, findings, scorecards, audit tools and the
  CI guard script.
- `.claude/agents/audit-*` - the auditor agent definitions (`.claude/agents/audit-*.md`).

Four layers keep the factory off them:

1. **The board.** A task that writes an audit path is interactive only (`lane: no`,
   BL-993). `task-board.ps1` refuses, for a dark factory process only, to file a
   lane-claimable task that touches an audit path, and to claim an interactive-only task
   (BL-996).
2. **The hook.** Every dark factory process carries the environment variable
   `CURL_DARK_FACTORY_LANE` (BL-995). While it is set, a PreToolUse hook refuses Edit,
   Write, MultiEdit and NotebookEdit calls on an audit path, and Bash and PowerShell
   calls that name one (BL-997).
3. **CI.** CI fails when `work/dark-factory` changed an audit path since its merge base
   with `master` (BL-998). Red CI blocks the shift-end merge, and the coordinator's CI
   watch files an interactive-only task for the failure (BL-999).
4. **The branch and Stewart.** Audit sessions work on their own `audit` branch. Findings
   and scorecards reach `master` only through a pull request. Amended 2026-09-30: Stewart
   gave a standing exception ("stop asking redundant questions and just do the job without
   me"), so an interactive session merges these pull requests itself once CI passed on all
   three platforms for the head commit, and reports afterwards. The factory still cannot:
   the merge is done by an interactive session, never a lane. Each scorecard records a SHA-256
   fingerprint of the auditor definitions it ran with (BL-1002), so a changed auditor
   shows in the scorecard.

### 2. Six read-only specialist auditors, one job each

| Auditor | Job | Model |
| --- | --- | --- |
| quality | Tests that do not check what their name claims, weak assertions, hand-built mutation testing | Sonnet |
| security | Hand-built crypto, TLS and SSH: fuzzing hostile input, timing leaks, secrets reaching logs | Opus |
| performance | Speed, memory and startup against real curl on the same transfers; the native AOT build | Sonnet |
| conformance | Differential testing of real curl against Curl on generated command lines, building on `conformance-auditor` and `Record-CurlExchange.ps1` | Opus |
| truthfulness | Names, docs and ADRs against the code; reports only - `align-and-document` still owns fixing | Sonnet |
| process | The factory's logs in `<repo>.logs` and git history: time per task, redone work, time with CI red, idle lanes, overlap-caused serialisation, token cost | Sonnet |

The planted-defect seeder (point 4) also runs on Sonnet. Every auditor is read-only.
Auditors judge the code before reading the factory's ADRs and task logs, so they do not
inherit its reasoning.

### 3. Findings

Findings live in `Audit/Findings/`, each with a severity, evidence and a reproduction. A
triage step Stewart approves turns accepted findings into Curl tasks. A finding closes
only when a re-audit confirms the fix - never because a factory task reached Done.

### 4. Planted defects

Before each audit a seeding agent plants 5 to 10 known defects from a catalogue on a
throwaway detached worktree. Each auditor's catch rate is reported beside its findings,
and an auditor that misses a defect planted for it has its findings flagged unreliable in
that scorecard.

### 5. Scorecard and cadence

Each audit produces one scorecard in a fixed format, so trends show from one audit to the
next. Audits run on demand, before each roadmap-milestone merge to `master`, and after
changes to `RunDarkFactory.ps1`. They run between shifts, never during one, because they
spend the same Claude budget. They start with `Audit\RunAudit.cmd -NewTab`, in herdr like
the factory.

### 6. Tooling rules

Audit tools are PowerShell or C# file-based apps only (no Python), base class library
only, no package. Audit tools under `Audit/Tools/` are not product code and are not held
to the coverage gates.

### 7. The audit check is required on master (BL-1029)

GitHub runs the workflow file of the branch being built, so the `audit-guard` job exists
on `work/dark-factory` only while that branch's own `ci.yml` defines it: deleting the job
would remove the check that catches the deletion. The repository ruleset "Audit guard
required on master" (id 24235022, Stewart approved 2026-09-30) therefore requires the
status check `Audit paths untouched by the dark factory` on every change to `master`, so
a missing or failing check blocks the merge instead of passing it. It has no bypass
actors: the dark factory merges with Stewart's GitHub credentials, so an admin bypass
would let a lane merge with `gh pr merge --admin`. On any other pull request the job is
skipped by its condition, and GitHub counts a skipped required check as passed.
Stewart changes or removes the rule in the repository's settings.

### 8. The planted-defect catalogue is stored encoded (BL-1028)

The PreToolUse hook stops a lane reading `Audit/` directly, but a search over the repository
root - which lanes run constantly - still reaches it. The instructions and findings being
seen is tolerable; the catalogue of planted defects is not, because a lane that read it could
avoid exactly those defects and make the catch rates meaningless. So the catalogue is
committed base64-encoded, `Audit/PlantedDefects/Catalogue.md.b64`, and only the
`audit-seeder` decodes it, with `Read-PlantedCatalogue.ps1`. Keeping it only on the `audit`
branch would not hold once audit pull requests merge routinely (section 4's amendment), and
generating defects fresh each audit would lose the fixed catalogue scorecards compare
against. Decided by Claude under Stewart's delegation, 2026-09-30.

## Rationale

- **Guards outside the factory's reach, approval by Stewart.** The factory is the party
  audited, so anything it can edit it can weaken. Four independent layers mean a single
  gap - a hook bypassed, a task mis-flagged - does not open the audit paths, and a
  per-merge approval by Stewart replaces the standing exceptions the factory enjoys
  elsewhere.
- **Planted defects.** A clean report alone cannot show whether an auditor would have
  noticed a defect; a known defect it missed can.
- **Code before rationale.** Reading the factory's ADRs and logs first would let an
  auditor adopt a wrong justification; judging the code first keeps its view its own.
- **Close on re-audit.** "Task Done" is the factory's claim; only a re-audit shows the
  defect is gone.
- **Models.** Security and conformance need the deepest reasoning (hand-built crypto,
  byte-level differential behaviour), so they run on Opus; the other four and the seeder
  do bounded, pattern-shaped work on Sonnet to spend less of the shared budget.

## Consequences

- The factory cannot file, claim or make changes to audit work; every change to `Audit/`
  or an auditor definition is an interactive session, which is slower by design.
- Audit results reach `master` only as fast as Stewart approves pull requests from the
  `audit` branch.
- Audits compete with shifts for the same Claude budget, so they are scheduled between
  shifts and at milestones rather than continuously.
- Scorecards are comparable over time, and a changed auditor is visible through its
  fingerprint.

## Alternatives considered

- **Auditors as ordinary factory tasks.** Lost: the factory would be able to edit its own
  auditors and findings.
- **A standing exception for merging the `audit` branch.** Lost: it would hand the final
  gate back to automation.
- **Closing findings when their fix task reaches Done.** Lost: it trusts the audited
  party's own claim.
- **One general auditor.** Lost: one job per auditor keeps each prompt focused and makes
  its planted-defect catch rate meaningful.
