---
id: BL-2123
title: Fix AF-0177: ADR-0475 says only wrapped Note: and Warning: lines are joined; Unwrap also joins wrapped curl: error lines
priority: Low
assignee: Claude
pipeline: docs
depends-on: []
touches: [Documentation]
requirement: none
created: 2026-10-13
completed:
---
# BL-2123 — Fix AF-0177: ADR-0475 says only wrapped Note: and Warning: lines are joined; Unwrap also joins wrapped curl: error lines

## Goal

The defect the audit office reported as AF-0177 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0177 (Low, truthfulness auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0177-adr-0475-says-only-wrapped-note-and-warning-lines.md`.

Location: `Documentation/Planning/Decisions/ADR-0475-upstream-case-stderr-naming-logdir-is-compared-with-wrapped-notes-joined.md:33`

Location: `Documentation/Planning/Decisions/ADR-0475-upstream-case-stderr-naming-logdir-is-compared-with-wrapped-notes-joined.md:33`

ADR-0475 decision: 'A line is joined to the next when both start with the same `Note: ` or `Warning: ` prefix and the first ends in a blank or tab or is at least 79 characters long'. The ADR title, the ADR index row (Documentation/Planning/Decisions/README.md:508) and Curl.Conformance.UnitLibrary/CLAUDE.md:233 say the same thing ('curl's 79-column-wrapped `Note:` and `Warning:` lines joined'). The code does more. Curl.Conformance.UnitLibrary/UpstreamWrappedMessageLines.cs:24 has Prefixes = ["Warning: ", "Note: ", "curl: "], and its summary covers test411's wrapped 'curl: cannot read config from' error. UpstreamWrappedMessageLinesTests pins the joining of curl: lines. The substance (join wrapped lines when stderr names %LOGDIR) is true; the list of prefixes is incomplete.

Reproduction, from the finding:

Run from the repository root:

```powershell
(Select-String -Path Curl.Conformance.UnitLibrary/UpstreamWrappedMessageLines.cs -Pattern 'Prefixes = \[.*"curl: "').Count; (Select-String -Path Documentation/Planning/Decisions/ADR-0475-*.md -Pattern 'curl: ').Count
```

- Expected: When the first count is 1, the second is at least 1: the ADR mentions that curl: lines are joined too
- Actual: 1 then 0: the code joins curl: lines and the ADR never mentions them

The finding closes only when a later re-audit by the truthfulness auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-13: Created.
- 2026-10-12: Backlog -> Doing.
