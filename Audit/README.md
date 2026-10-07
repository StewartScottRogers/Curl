# Audit office

The audit office is a team of independent AI auditors that, from time to time, audit the
dark factory and the code it produced for gaps in quality, security, performance,
conformance, truthfulness and process. The factory checks its own work; the office exists
so that something that did not write the code, and cannot be changed by what did, checks it
too. The design is [ADR-0267](../Documentation/Planning/Decisions/ADR-0267-an-independent-audit-office-audits-the-dark-factory-from-outside-its-reach.md).

Every part below exists; the last column names the task that built it.

## Folder map

| Path | What it holds | Built by |
| --- | --- | --- |
| `Guard/` | `Test-AuditPathsUntouched.ps1`, the CI audit guard: fails `CI` when `work/dark-factory` changes an audit path or a guard. | BL-998 |
| `Instructions/` | Each auditor's method (`Quality.md`, `Security.md`, `Performance.md`, `Conformance.md`, `Truthfulness.md`, `Process.md`), the rules every auditor follows (`Auditor-Rules.md`) and the finding, report and scorecard formats (`Report-Format.md`). | BL-1001, BL-1009 to BL-1014 |
| `PlantedDefects/` | The catalogue of known defects the seeder plants before an audit, to measure each auditor's catch rate - stored encoded (`Catalogue.md.b64`) so no search of the repository returns it - with `Read-PlantedCatalogue.ps1`. | BL-1015, BL-1028 |
| `Findings/` | One file per finding, `AF-####-*.md`, with severity, evidence and a reproduction. A finding closes only on evidence: its auditor's re-audit backed by the run's own mechanical rerun, a reliable auditor or two re-audits running; or as a duplicate, or by Stewart ([ADR-0422](../Documentation/Planning/Decisions/ADR-0422-audit-findings-close-on-mechanical-evidence.md)). | BL-1001, BL-1016 |
| `Scorecards/` | One scorecard per audit, `yyyy-MM-dd_HHmm.md`, in a fixed format so trends show, with each auditor's catch rate and a fingerprint of the auditor definitions it ran with. | BL-1001, BL-1017 |
| `Tools/` | Audit tooling: `Get-AuditorFingerprint.ps1`, `Invoke-MutationTest.ps1`, `Find-WeakTests.ps1`, `Find-BooleanNameMismatches.ps1`, `Fuzz/`, `Invoke-DifferentialConformance.ps1`, `Measure-Performance.ps1`, `Measure-FactoryProcess.ps1`, `Write-AuditFindings.ps1`, `Write-AuditScorecard.ps1`, `New-TasksFromAcceptedFindings.ps1`, `Test-AuditDue.ps1`, and `Fixtures/` for their self-tests. Not product code, not held to the coverage gates. | BL-1002 to BL-1008, BL-1016 to BL-1019 |
| `Triage.md` | How findings become Curl tasks: Stewart accepts or rejects each, then `New-TasksFromAcceptedFindings.ps1` files a task for each accepted one, and a `Re-fix` task when a fix did not hold. | BL-1018 |
| `RunAudit.cmd`, `RunAudit.ps1` | Run an audit end to end, in a herdr tab (`-NewTab`); `-DryRun` prints the plan. | BL-1020 |
| `../.claude/agents/audit-*.md` | The auditor agents: `audit-quality`, `audit-security`, `audit-performance`, `audit-conformance`, `audit-truthfulness`, `audit-process`, and `audit-seeder`, which plants the defects. | BL-1009 to BL-1015 |

## Independence

**Interactive only.** Every task that writes to `Audit/` or `.claude/agents/audit-*` is
interactive only: it carries `lane: no`, or touches an audit path, so `task-board.ps1`
never offers it to a dark factory lane and refuses a lane that tries to claim or file one
(BL-993, BL-996). An interactive session runs it by name, `/task-run BL-###`.

**The hook.** Every dark factory process carries `CURL_DARK_FACTORY_LANE` (BL-995). While it
is set, the PreToolUse hook `.claude/hooks/guard-audit-paths.ps1` refuses any tool call that
reads or changes an audit path (BL-997), so a lane cannot read the planted defects or the
auditors' instructions, nor edit them.

**The CI guard.** `Guard/Test-AuditPathsUntouched.ps1` fails the `CI` workflow's
`audit-guard` job whenever `work/dark-factory` carries a change of its own to an audit path,
or to one of the guards themselves: the hook, `.claude/settings.json`, `ci.yml` and
`task-board.ps1` (BL-998). CI runs `master`'s copy of the script, `master` requires the check
(BL-1029), and a red run blocks the shift-end merge; the CI watch files the failure as an
interactive-only task (BL-999).

**The `audit` branch.** Audit work is done on the `audit` branch, cut
from `master`, never on the factory's branch. It reaches `master` only through a pull
request, which an interactive session merges once CI is green on all three platforms (Stewart's
standing exception, 2026-09-30, in CLAUDE.md and ADR-0267); a dark factory lane never does. The
factory branch then takes it by merging `master`.
