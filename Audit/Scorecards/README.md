# Audit scorecards

One scorecard per audit: what was audited, by which auditor definitions, what each
auditor found, how many of the defects planted for it it caught, the performance and
process numbers, and how all of that changed since the previous audit. Every scorecard
has the same sections, rows and columns in the same order, so two scorecards diff line
by line and a trend shows as a changed cell. The design is
[ADR-0267](../../Documentation/Planning/Decisions/ADR-0267-an-independent-audit-office-audits-the-dark-factory-from-outside-its-reach.md).

`Audit/Tools/Write-AuditScorecard.ps1` (BL-1017) writes each scorecard from
[SCORECARD-TEMPLATE.md](SCORECARD-TEMPLATE.md), the auditors' report blocks
([Report-Format.md](../Instructions/Report-Format.md)), the planted-defect manifest and
[the findings](../Findings/README.md). Auditors never read this folder
([Auditor-Rules.md](../Instructions/Auditor-Rules.md), rule 3).

## File name

`yyyy-MM-dd_HHmm.md`, the local date and time the audit started, for example
`2026-10-14_0930.md`. Names sort in time order, so the previous scorecard is the file
whose name sorts immediately before this one's.

## Sections

In this order, with these exact headings. Nothing is added, dropped or reordered, and a
value that is missing is written as a marker (see [Cell values](#cell-values)) rather
than leaving its row out.

| # | Section | Holds |
| --- | --- | --- |
| 1 | Header (`# Audit scorecard <date> <time>` and its table) | Date, audited branch, audited commit (full SHA), auditor fingerprint (from `Audit/Tools/Get-AuditorFingerprint.ps1`, planned BL-1002), planted-defect count, duration in minutes, token cost of the whole audit in US dollars. |
| 2 | `## Attention` | What Stewart should look at first ([Attention](#attention)): stuck findings and auditors left out with accepted findings, or `None.`. |
| 3 | `## Auditors` | One row per auditor, in the fixed order quality, security, performance, conformance, truthfulness, process (see [Auditor table](#auditor-table)). |
| 4 | `## Performance` | The performance auditor's metrics: one row per scenario in the fixed order `startup`, `small-get`, `large-get`, `headers-verbose`, `chunked`, `redirects`, then the native AOT binary's size. |
| 5 | `## Process` | The process auditor's metrics, one row each, in the order [Report-Format.md](../Instructions/Report-Format.md#process) lists them. |
| 6 | `## Change since the previous scorecard` | The previous scorecard's name and whether the auditor fingerprint changed, then the numbers of sections 3, 4 and 5 as deltas, in the same rows and columns. |
| 7 | `## New findings` | A link to each finding file this audit created. |

## Auditor table

| Column | Value |
| --- | --- |
| Auditor | `quality`, `security`, `performance`, `conformance`, `truthfulness`, `process`, always in that order and always all six. |
| Model | The model the auditor ran on. ADR-0267 fixes `opus` for security and conformance and `sonnet` for the other four. |
| New Critical, New High, New Medium, New Low | Finding files this audit created for that auditor, by severity. A catch of a planted defect and a repeat of an open finding are not new findings. |
| Re-audited | Findings of that auditor its report re-audited. |
| Closed | Findings of that auditor this audit closed - by any path of the [closure rule](../Findings/README.md#closing), duplicates included - counted by the last scorecard in their `closed-by`. |
| Still open | That auditor's findings in `Audit/Findings/` that are open (`proposed`, `accepted`, `deferred` or `blocked`) after this audit, new ones included. |
| Planted assigned | Planted defects the manifest assigns to that auditor. |
| Planted caught | Of those, the ones it caught: its report has a finding in the defect's file whose title, key or evidence contains the manifest's catch text, or whose location is within 2 lines of the defect's line (a defect reported for two symptoms names its catch text once; BL-1316). Every such finding is a catch, and none is filed. A catch by a different auditor does not count for either. |
| Catch rate | Planted caught divided by planted assigned, as a whole percent. |
| Reliable | `yes` or `no`, see [Reliability](#reliability). |

## Reliability

An auditor is reliable in an audit - `Reliable: yes` - when it caught every planted
defect assigned to it (catch rate 100%) and returned a parseable report block. It is
unreliable - `Reliable: no` - when it missed any defect planted for it, returned no
parseable report block, or changed the audited tree
([Auditor-Rules.md](../Instructions/Auditor-Rules.md), rules 1 and 6).

Reliability gates how far an auditor's own word goes. Its catch rate is scored here, and its
new findings are believed only as far as it is reliable. A re-audit by an auditor whose row
says `Reliable: no` closes a finding only with evidence beside it - the audit run's own
mechanical rerun agreeing, or a second consecutive "no" from it on a later audit - never on its
word alone ([Findings/README.md](../Findings/README.md#closing), ADR-0422). Its new findings are still filed, with the
flag in their Summary, and every number in this scorecard that comes from its report -
its new-finding, re-audited, closed and still-open counts, and its metrics in the
performance or process table - is followed by ` (unreliable)`, here and in the deltas.

## Cell values

Numbers are written the same way every time, so two runs on the same inputs give the
same bytes: invariant culture, `.` as the decimal separator, no thousands separator.

| Kind | Format | Example |
| --- | --- | --- |
| Count, bytes, tokens | Whole number | `3`, `4718592` |
| Milliseconds, minutes | One decimal place | `41.2` |
| US dollars | Two decimal places | `12.40` |
| Catch rate | Whole percent | `100%`, `50%` |
| Date and time | `yyyy-MM-dd HH:mm`, local time | `2026-10-14 09:30` |

Markers, used instead of a number:

| Marker | Meaning |
| --- | --- |
| `not run` | The auditor was not selected for this audit; every cell of its row after Auditor, and every cell of its metrics table. |
| `-` | No value: the report did not carry the metric or carried `null`, planted assigned is 0 (catch rate), or a delta whose either side is not a number. |

## Change since the previous scorecard

- Previous scorecard: a link to the newest earlier scorecard in this folder, or
  `first scorecard` when there is none, in which case every delta cell is `-`.
- Auditor fingerprint: `same` or `changed` against the previous scorecard's, so a change
  to the auditors themselves is visible beside the numbers it may have moved.
- Each delta is this scorecard's number minus the previous one's, in the same format as
  the number, with a sign: `+3`, `-1.5`, `0`. A catch-rate delta is in percentage
  points: `+50`.

## Attention

One line per item, stuck findings first, then auditors left out, or `None.` when there are none:

```
- Stuck: [AF-0005](../Findings/AF-0005-slug.md) - quality - accepted and still open after 4 audits that ran its auditor; task BL-1261. Stewart may close it (closed-how: stewart) or have it re-fixed. <title>
- Not run: quality has 15 accepted findings this audit did not re-audit: AF-0005, AF-0006, ...
```

A finding is **stuck** when it is `accepted`, and more than 3 audits that ran its auditor -
scorecards after the one that found it, dated on or after the day it was accepted, this one
included - have not closed it. An auditor is **left out** when this audit did not run it
(`-Auditors`) and it has accepted findings, which therefore went un-re-audited.

## New findings

One line per finding file this audit created, in ID order, or `None.` when there are
none:

```
- [AF-0012](../Findings/AF-0012-slug.md) - High - security - <title>
```

## Placeholders

[SCORECARD-TEMPLATE.md](SCORECARD-TEMPLATE.md) marks every value as `{{NAME}}`, and the
writer replaces every one. Names follow one rule, so a tool can derive them:

| Placeholder | Holds |
| --- | --- |
| `{{DATE}}`, `{{TIME}}` | The audit's start, `yyyy-MM-dd` and `HH:mm`. |
| `{{BRANCH}}`, `{{COMMIT}}` | The audited branch and the full SHA of the audited commit. |
| `{{FINGERPRINT}}` | The auditor fingerprint. |
| `{{PLANTED_COUNT}}` | Defects planted for this audit. |
| `{{DURATION_MINUTES}}` | Minutes from start to finish, whole number. |
| `{{AUDIT_COST_USD}}` | The token cost of the whole audit in US dollars. |
| `{{<AUDITOR>_<COLUMN>}}` | An auditor-table cell: `QUALITY_MODEL`, `SECURITY_NEW_HIGH`, `PROCESS_CATCH_RATE`. Columns: `MODEL`, `NEW_CRITICAL`, `NEW_HIGH`, `NEW_MEDIUM`, `NEW_LOW`, `REAUDITED`, `CLOSED`, `STILL_OPEN`, `PLANTED_ASSIGNED`, `PLANTED_CAUGHT`, `CATCH_RATE`, `RELIABLE`. |
| `{{<METRIC>}}` | A metric named in [Report-Format.md](../Instructions/Report-Format.md#metrics), converted: `.` and `-` become `_`, an `_` goes before each upper-case letter, then all upper case. `small-get.curl.medianMs` is `{{SMALL_GET_CURL_MEDIAN_MS}}`, `costUsdPerTaskDone` is `{{COST_USD_PER_TASK_DONE}}`. |
| `{{PREVIOUS_SCORECARD}}`, `{{FINGERPRINT_CHANGE}}` | The first two lines of the change section. |
| `{{DELTA_<NAME>}}` | The delta of the number `{{<NAME>}}` holds. |
| `{{ATTENTION}}` | The lines of the attention section. |
| `{{NEW_FINDING_LINKS}}` | The lines of the new-findings section. |
