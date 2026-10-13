---
id: AF-0177
title: ADR-0475 says only wrapped Note: and Warning: lines are joined; Unwrap also joins wrapped curl: error lines
auditor: truthfulness
severity: Low
status: accepted
reason: 
key: truthfulness:Documentation/Planning/Decisions/ADR-0475-upstream-case-stderr-naming-logdir-is-compared-with-wrapped-notes-joined.md:UpstreamWrappedMessageLines.Unwrap:stale-adr
reproduction: none
task: BL-2123
tasks: BL-2123
found: 2026-10-12
found-at: 4e6ddbc5b33356117bfec6778ba550ab61454a99
scorecard: 2026-10-12_1653.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0177 - ADR-0475 says only wrapped Note: and Warning: lines are joined; Unwrap also joins wrapped curl: error lines

## Summary

Low finding from the truthfulness auditor at `Documentation/Planning/Decisions/ADR-0475-upstream-case-stderr-naming-logdir-is-compared-with-wrapped-notes-joined.md:33`: ADR-0475 says only wrapped Note: and Warning: lines are joined; Unwrap also joins wrapped curl: error lines. Reported by an auditor flagged unreliable in 2026-10-12_1653.md.

## Evidence

Location: `Documentation/Planning/Decisions/ADR-0475-upstream-case-stderr-naming-logdir-is-compared-with-wrapped-notes-joined.md:33`

ADR-0475 decision: 'A line is joined to the next when both start with the same `Note: ` or `Warning: ` prefix and the first ends in a blank or tab or is at least 79 characters long'. The ADR title, the ADR index row (Documentation/Planning/Decisions/README.md:508) and Curl.Conformance.UnitLibrary/CLAUDE.md:233 say the same thing ('curl's 79-column-wrapped `Note:` and `Warning:` lines joined'). The code does more. Curl.Conformance.UnitLibrary/UpstreamWrappedMessageLines.cs:24 has Prefixes = ["Warning: ", "Note: ", "curl: "], and its summary covers test411's wrapped 'curl: cannot read config from' error. UpstreamWrappedMessageLinesTests pins the joining of curl: lines. The substance (join wrapped lines when stderr names %LOGDIR) is true; the list of prefixes is incomplete.

## Reproduction

Run from the repository root:

```powershell
(Select-String -Path Curl.Conformance.UnitLibrary/UpstreamWrappedMessageLines.cs -Pattern 'Prefixes = \[.*"curl: "').Count; (Select-String -Path Documentation/Planning/Decisions/ADR-0475-*.md -Pattern 'curl: ').Count
```

- Expected: When the first count is 1, the second is at least 1: the ADR mentions that curl: lines are joined too
- Actual: 1 then 0: the code joins curl: lines and the ADR never mentions them

## Re-audits

- 2026-10-13 | 2026-10-13_0925.md | reproduces: no | Ran the reproduction: Prefixes with 'curl: ' in UpstreamWrappedMessageLines.cs = 1 (line 25: ["Warning: ", "Note: ", "curl: "]); 'curl: ' in ADR-0475 = 1. ADR-0475 line 34 now names the curl: prefix, citing test411's wrapped 'curl: cannot read config from' error. Residual imprecision: the ADR title and line 27 still mention only Note: and Warning: lines.

## Log

- 2026-10-12: filed proposed.
- 2026-10-12: proposed -> accepted.
