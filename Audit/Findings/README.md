# Audit findings

One file per finding an auditor reported: what is wrong, the evidence, a reproduction
anyone can run, and every re-audit since. A finding is the audit office's claim about the
audited code or process; it becomes Curl work only when Stewart accepts it and triage
turns it into a task. The design is
[ADR-0267](../../Documentation/Planning/Decisions/ADR-0267-an-independent-audit-office-audits-the-dark-factory-from-outside-its-reach.md).

`Audit/Tools/Write-AuditFindings.ps1` (BL-1016) writes and updates these files
from the auditors' report blocks ([Report-Format.md](../Instructions/Report-Format.md)).
Auditors never read this folder ([Auditor-Rules.md](../Instructions/Auditor-Rules.md),
rule 3); the findings they are asked to re-audit are given to them in their prompt.

## File name and ID

- File name: `AF-####-<slug>.md`, for example `AF-0007-weak-assertion-in-parse-empty.md`.
  `####` is the ID's number, four digits, zero-padded. `<slug>` is the title in lower
  case, words joined by `-`, ASCII letters, digits and `-` only.
- IDs run in one sequence across all auditors. A new finding takes the next number after
  the highest in this folder. An ID is never reused: a rejected or closed finding keeps
  its file and its number.
- The file is created from [FINDING-TEMPLATE.md](FINDING-TEMPLATE.md) and keeps its front
  matter and section order exactly. Every `{{NAME}}` placeholder in the template is
  replaced; [Report-Format.md](../Instructions/Report-Format.md#from-report-to-finding)
  says which report field fills which placeholder.

## Front matter

Every key is present, lower case, one per line, in this order, written `key: value` with
the value unquoted on the same line. A key with no value yet is written with nothing
after the colon (`closed:`). Findings closed or rejected before ADR-0422 (2026-10-07) lack
`reproduction`, `tasks`, `duplicate-of` and `closed-how`, since closed and rejected findings
are never edited; a missing key reads as empty, and the tools insert it in its place when they
set it.

| Key | Value |
| --- | --- |
| `id` | `AF-####`, the same as the file name. |
| `title` | One line: what is wrong and where. |
| `auditor` | The auditor that reported it: `quality`, `security`, `performance`, `conformance`, `truthfulness` or `process`. |
| `severity` | `Critical`, `High`, `Medium` or `Low` (see [Severities](#severities)). |
| `status` | `proposed`, `accepted`, `deferred`, `blocked`, `rejected` or `closed` (see [Statuses](#statuses)). |
| `reason` | One line: why the latest status move was made. Empty for a `proposed` finding that has not moved. |
| `key` | The stable dedupe key, as defined in [Report-Format.md](../Instructions/Report-Format.md#the-key). A surviving mutant's is built mechanically from its site: `quality:<file>:<member>-<operator word>:surviving-mutant`. |
| `reproduction` | A reproduction the audit run can rerun itself: `mutation <file>:<line>:<operator>` (one mutant, run with `Invoke-MutationTest.ps1 -Site`), or `none`. The line is where the mutant was last known; an interactive session may move it when the code moves (ADR-0422). |
| `task` | The current Curl task for it: the newest of its tasks still open, else the one last filed; `BL-###`, or `none`. |
| `tasks` | Every task filed for it, oldest first, comma-separated: its `Fix` task, any `Re-fix` tasks, and a duplicate's tasks. Empty while `task` names its only task. |
| `found` | The date of the audit that found it, `yyyy-MM-dd`. |
| `found-at` | The full 40-character SHA of the commit that audit audited. |
| `scorecard` | The file name of the scorecard of the audit that found it, `yyyy-MM-dd_HHmm.md`. |
| `duplicate-of` | The ID of the finding this one duplicates, set when it closes as a duplicate; empty otherwise. |
| `closed` | The date it was closed, `yyyy-MM-dd`; empty while it is not closed. |
| `closed-how` | How it closed ([Closing](#closing)): `mechanical`, `reliable-reaudit`, `consecutive`, `duplicate` or `stewart`; empty while it is not closed (and on findings closed before ADR-0422, all by a reliable re-audit). |
| `closed-by` | What closed it: the re-audit's scorecard; both scorecards, comma-separated, for `consecutive`; the scorecard of the audit that found the duplicate, or `session` for one an interactive session marked; `stewart` for Stewart's close. Empty while it is not closed. |

## Severities

Each auditor's instructions (`Audit/Instructions/<Auditor>.md`) may fix which severity a
given kind of finding gets; they apply these definitions and never replace them.

| Severity | Definition |
| --- | --- |
| `Critical` | Hostile input or an ordinary run can compromise the user - code execution, a leaked secret or key, certificate or host-key verification bypassed - or Curl reports success while writing wrong bytes, or the native AOT binary that ships cannot be built. |
| `High` | Curl stops being a drop-in replacement on a common path (a wrong exit code, output or request byte, a crash), or a check the factory relies on passes while the thing it names is broken. |
| `Medium` | A real defect with limited reach: an uncommon option or path differs from curl, a measurable loss of speed, memory or factory time, or a name or document that states something false. |
| `Low` | A defect no user or script would notice: a weak assertion beside a stronger one, a small inefficiency, or an imprecise name or comment. |

## Statuses

| Status | Meaning | Who sets it |
| --- | --- | --- |
| `proposed` | Reported by an auditor and not yet triaged. | `Write-AuditFindings.ps1`, when it files the finding. |
| `accepted` | Stewart agrees it is a defect; triage turns it into a task. | Stewart only. |
| `deferred` | Stewart has not decided yet and parked it, for example to understand it first. | Stewart only. |
| `blocked` | Stewart cannot decide until something else happens; `reason` names it. | Stewart only. |
| `rejected` | Stewart does not agree it is a defect, or does not want it fixed. | Stewart only. |
| `closed` | Evidence showed the reproduction no longer reproduces, it duplicates another finding, or Stewart closed it ([Closing](#closing)). | `Write-AuditFindings.ps1` under the closure rule, a session marking a duplicate, or Stewart. |

A finding is **open** while it is `proposed`, `accepted`, `deferred` or `blocked`.

### Moves

Stewart's moves are exactly these (Stewart, 2026-10-02, BL-1183). Rejecting is only from `deferred`, so nothing is rejected straight from a first read.

| From | To |
| --- | --- |
| `proposed` | `accepted`, `deferred` or `blocked` |
| `deferred` | `proposed` (back for another look) or `rejected` |
| `blocked` | `proposed` |
| `accepted` | `closed`, with `closed-how: stewart` and `closed-by: stewart` (ADR-0422) |

Stewart's close is his alone - Claude never makes it on its own judgement, however stuck a
finding looks; the scorecard's Attention section lists stuck findings so he can decide. Any
other move to `closed` is by rule 3. `rejected` and `closed` have no move of Stewart's out of
them.

Every move sets `reason` and appends one line to `## Log`:

```n- 2026-10-02: proposed -> deferred. Not sure what the reproduction shows; read it later.
```

The date, the old and new status, and the reason.

## Rules

1. A finding arrives `proposed`.
2. Only Stewart sets `accepted`, `deferred`, `blocked`, `rejected` or `proposed` again, by the moves above:
   in the audit pull request, on the board page's Audit tab (BL-1185), or by telling a session.
3. A finding becomes `closed` only on evidence, by one of the paths in [Closing](#closing) -
   never because its task reached Done. Nothing else closes a finding: not its task's
   state, and not an audit in which it was not reported.
4. A closed finding that reappears is a new finding naming the old one: a new ID whose
   Summary says "reappeared; previously AF-####". The closed file is not reopened.
5. A report whose `key` equals the `key` of an open finding is the same finding: it adds a
   `Re-audits` line saying it was still reported, keeps its status, and files no new file.
   A surviving mutant's key is built from its site, so the same mutant in other words is the
   same finding; open findings that share a key are one finding, and all but the lowest ID
   close as duplicates of it.
6. A finding reported by an auditor flagged unreliable in that audit is still filed, and
   its Summary says so.
7. A report that matches a planted defect is a catch, not a finding, and is never filed
   here; it counts toward the auditor's catch rate on the scorecard.
8. `closed` findings are never edited again. A `rejected` finding is never edited either,
   except that a report repeating its `key` adds a `Re-audits` line saying it was still
   reported; its status stays `rejected` (Decided by Claude, 2026-09-30, BL-1016).

## Closing

Decided in [ADR-0422](../../Documentation/Planning/Decisions/ADR-0422-audit-findings-close-on-mechanical-evidence.md).
An open finding closes when its own auditor's re-audit says the reproduction no longer
reproduces and the first of these holds; `closed-how` records which:

| `closed-how` | When |
| --- | --- |
| `mechanical` | The finding has a mechanical `reproduction` and the audit run's own rerun of it on the clean audited commit - never the planted tree - agrees: the targeted mutant is killed. This closes whatever the auditor's reliability on that scorecard. A rerun in which the mutant survives closes nothing, by any path. |
| `reliable-reaudit` | No mechanical answer (`reproduction: none`, or the rerun could not tell) and the auditor is reliable on that audit's scorecard ([Scorecards/README.md](../Scorecards/README.md#reliability)). |
| `consecutive` | No mechanical answer, and the finding's previous `Re-audits` line is also its own auditor's `reproduces: no`, from a different audit. `closed-by` names both scorecards. |

Two more closes are not re-audits:

| `closed-how` | When |
| --- | --- |
| `duplicate` | It has the same key as an open finding with a lower ID: `Write-AuditFindings.ps1` closes it at the start of an audit, or an interactive session marks it; `duplicate-of` names the original, whose `tasks` gains its tasks. |
| `stewart` | Stewart closes an accepted finding himself ([Moves](#moves)), for example one the scorecard lists as stuck. |

A verdict that cannot be trusted is recorded but counts as neither yes nor no: a re-audit with
`"reproduces": null`, and one that **overlaps a planted defect** - the finding's location is in
a project that depends (by project references, transitively) on the project of a defect
planted for that audit, or the auditor's evidence names the planted file. Auditors run their
reproductions in the planted tree, so such a verdict may measure the plant (the 2026-10-07
re-audits of AF-0009 and AF-0026 measured PD-303 and PD-103).

## Body

Five sections, in this order, with these exact headings:

| Section | Holds |
| --- | --- |
| `## Summary` | What is wrong, where, which auditor reported it and at what severity; the unreliable flag or the "reappeared; previously AF-####" note when rule 4 or 6 applies. |
| `## Evidence` | The location and what was observed there - quoted code, command output, measurement - and why it is a defect. |
| `## Reproduction` | One command run from the repository root, then the expected result (what a correct tree gives) and the actual result (what the audited tree gave). |
| `## Re-audits` | Append-only, one line per re-audit, oldest first. Empty when the finding is new. |
| `## Log` | Last, append-only, one dated line per status move, oldest first ([Moves](#moves)). A new finding's first line is `- <found>: filed proposed.` |

A `Re-audits` line has four fields separated by ` | `:

```
- 2026-10-14 | 2026-10-14_0930.md | reproduces: no | Select-String finds no match; the test now asserts exit code 2.
```

The date of the re-audit, its scorecard's file name, `reproduces: yes`, `reproduces: no` or
`not re-audited`, and the evidence: what running the reproduction showed, or "still
reported" when rule 5 added the line. Evidence a different auditor gave starts
`(re-audited by <auditor>)`; a mechanical rerun's result is appended (`Runner's targeted
mutation rerun: killed.`); a set-aside verdict reads `overlaps planted defect PD-### in
<file>, so the auditor's verdict (reproduces yes) is set aside: <evidence>`.
