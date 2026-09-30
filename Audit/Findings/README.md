# Audit findings

One file per finding an auditor reported: what is wrong, the evidence, a reproduction
anyone can run, and every re-audit since. A finding is the audit office's claim about the
audited code or process; it becomes Curl work only when Stewart accepts it and triage
turns it into a task. The design is
[ADR-0267](../../Documentation/Planning/Decisions/ADR-0267-an-independent-audit-office-audits-the-dark-factory-from-outside-its-reach.md).

`Audit/Tools/Write-AuditFindings.ps1` (planned, BL-1016) writes and updates these files
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
after the colon (`closed:`).

| Key | Value |
| --- | --- |
| `id` | `AF-####`, the same as the file name. |
| `title` | One line: what is wrong and where. |
| `auditor` | The auditor that reported it: `quality`, `security`, `performance`, `conformance`, `truthfulness` or `process`. |
| `severity` | `Critical`, `High`, `Medium` or `Low` (see [Severities](#severities)). |
| `status` | `proposed`, `accepted`, `rejected` or `closed` (see [Statuses](#statuses)). |
| `key` | The auditor's stable dedupe key, as defined in [Report-Format.md](../Instructions/Report-Format.md#the-key). |
| `task` | The Curl task triage made from it, `BL-###`, or `none`. |
| `found` | The date of the audit that found it, `yyyy-MM-dd`. |
| `found-at` | The full 40-character SHA of the commit that audit audited. |
| `scorecard` | The file name of the scorecard of the audit that found it, `yyyy-MM-dd_HHmm.md`. |
| `closed` | The date it was closed, `yyyy-MM-dd`; empty while it is not closed. |
| `closed-by` | The file name of the re-audit's scorecard that closed it; empty while it is not closed. |

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
| `rejected` | Stewart does not agree it is a defect, or does not want it fixed. | Stewart only. |
| `closed` | A re-audit confirmed the reproduction no longer reproduces. | `Write-AuditFindings.ps1`, under the closure rule below. |

## Rules

1. A finding arrives `proposed`.
2. Only Stewart sets `accepted` or `rejected`, in the audit pull request or by telling a
   session.
3. A finding becomes `closed` only when a re-audit by the same auditor, not flagged
   unreliable, reports the reproduction no longer reproduces - never because its task
   reached Done. "Flagged unreliable" means that auditor's row in the re-audit's
   scorecard says `Reliable: no` ([Scorecards/README.md](../Scorecards/README.md#reliability)).
   Nothing else closes a finding: not its task's state, and not an audit in which it
   was not reported.
4. A closed finding that reappears is a new finding naming the old one: a new ID whose
   Summary says "reappeared; previously AF-####". The closed file is not reopened.
5. A report whose `key` equals the `key` of an open (`proposed` or `accepted`) finding
   is the same finding: it adds a `Re-audits` line saying it was still reported, and no
   new file.
6. A finding reported by an auditor flagged unreliable in that audit is still filed, and
   its Summary says so.
7. A report that matches a planted defect is a catch, not a finding, and is never filed
   here; it counts toward the auditor's catch rate on the scorecard.
8. `rejected` and `closed` findings are never edited again.

## Body

Four sections, in this order, with these exact headings:

| Section | Holds |
| --- | --- |
| `## Summary` | What is wrong, where, which auditor reported it and at what severity; the unreliable flag or the "reappeared; previously AF-####" note when rule 4 or 6 applies. |
| `## Evidence` | The location and what was observed there - quoted code, command output, measurement - and why it is a defect. |
| `## Reproduction` | One command run from the repository root, then the expected result (what a correct tree gives) and the actual result (what the audited tree gave). |
| `## Re-audits` | Append-only, one line per re-audit, oldest first. Empty when the finding is new. |

A `Re-audits` line has four fields separated by ` | `:

```
- 2026-10-14 | 2026-10-14_0930.md | reproduces: no | Select-String finds no match; the test now asserts exit code 2.
```

The date of the re-audit, its scorecard's file name, `reproduces: yes` or
`reproduces: no`, and the evidence: what running the reproduction showed, or "still
reported" when rule 5 added the line.
