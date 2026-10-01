# Audit office

The audit office is a team of independent AI auditors that, from time to time, audit the
dark factory and the code it produced for gaps in quality, security, performance,
conformance, truthfulness and process. The factory checks its own work; the office exists
so that something that did not write the code, and cannot be changed by what did, checks it
too. The design is [ADR-0267](../Documentation/Planning/Decisions/ADR-0267-an-independent-audit-office-audits-the-dark-factory-from-outside-its-reach.md).

Most of the office is still being built. Everything below marked *planned* names the task
that builds it; the rest exists today.

## Folder map

| Path | What it holds | Status |
| --- | --- | --- |
| `Guard/` | `Test-AuditPathsUntouched.ps1`, the CI audit guard: fails `CI` when `work/dark-factory` changes an audit path or a guard. | Built (BL-998) |
| `Instructions/` | Each auditor's method (`Quality.md`, `Security.md`, `Performance.md`, `Conformance.md`, `Truthfulness.md`, `Process.md`), the rules every auditor follows (`Auditor-Rules.md`) and the finding, report and scorecard formats (`Report-Format.md`). | Planned: formats and rules BL-1001; methods BL-1009 to BL-1014 |
| `PlantedDefects/` | The catalogue of known defects the seeder plants before an audit, to measure each auditor's catch rate. | Planned (BL-1015, kept out of lanes' reach by BL-1028) |
| `Findings/` | One file per finding, with severity, evidence and a reproduction. A finding closes only when a re-audit confirms the fix. | Planned: format BL-1001, writing and closing BL-1016 |
| `Scorecards/` | One scorecard per audit, in a fixed format so trends show, with each auditor's catch rate and a fingerprint of the auditor definitions it ran with. | Planned: format BL-1001, writing BL-1017 |
| `Tools/` | Audit tooling: `Get-AuditorFingerprint.ps1` (BL-1002), `Invoke-MutationTest.ps1` (BL-1004), `Fuzz/` (BL-1005), `Invoke-DifferentialConformance.ps1` (BL-1006), `Measure-Performance.ps1` (BL-1007), `Measure-FactoryProcess.ps1` (BL-1008), `Write-AuditFindings.ps1` (BL-1016), `Write-AuditScorecard.ps1` (BL-1017), `New-TasksFromAcceptedFindings.ps1` (BL-1018), `Test-AuditDue.ps1` (BL-1019). Not product code, not held to the coverage gates. | Planned (task IDs beside each) |
| `Triage.md` | Which findings Stewart accepted, and the Curl tasks they became. | Planned (BL-1018) |
| `RunAudit.cmd`, `RunAudit.ps1` | Run an audit end to end, in a herdr tab. | Planned (BL-1020) |
| `../.claude/agents/audit-*.md` | The auditor agents: `audit-quality`, `audit-security`, `audit-performance`, `audit-conformance`, `audit-truthfulness`, `audit-process`, and `audit-seeder`, which plants the defects. | Planned (BL-1009 to BL-1015) |

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
