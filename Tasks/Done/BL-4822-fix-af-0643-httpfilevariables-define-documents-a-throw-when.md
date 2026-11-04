---
id: BL-4822
title: Fix AF-0643: HttpFileVariables.Define documents a throw when the value 'references no variable'; it throws only for a reference to an undefined variable
priority: Low
assignee: Claude
pipeline: docs
depends-on: []
touches: [Curl.Extension.HttpFile.UnitLibrary]
requirement: none
created: 2026-11-04
completed: 2026-11-04
---
# BL-4822 — Fix AF-0643: HttpFileVariables.Define documents a throw when the value 'references no variable'; it throws only for a reference to an undefined variable

## Goal

The defect the audit office reported as AF-0643 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0643 (Low, truthfulness auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0643-httpfilevariables-define-documents-a-throw-when-th.md`.

Location: `Curl.Extension.HttpFile.UnitLibrary/HttpFileVariables.cs:30`

Location: `Curl.Extension.HttpFile.UnitLibrary/HttpFileVariables.cs:30`

Define's doc: '<exception cref="ArgumentExpansionException">The value references no variable (exit 2).</exception>'. That reads as a throw for a value with no {{reference}}. The code is Define => values[name] = Substitute(value). Substitute appends a value with no {{...}} unchanged, and ValueOf throws only for a reference whose name is not in values ('http-file: variable ... is not defined', CurlExitCode.FailedInit). Substitute's own doc says it correctly: 'A reference names no variable (exit 2).' Sampled method (seed 221035).

Reproduction, from the finding:

Run from the repository root:

```powershell
Select-String -Path Curl.Extension.HttpFile.UnitLibrary/HttpFileVariables.cs -Pattern 'references no variable|names no variable|is not defined'
```

- Expected: Define's exception doc says the throw is for a reference that names no defined variable, as Substitute's does.
- Actual: Line 30 says 'The value references no variable (exit 2)', while the throw (line ~68) is for an undefined referenced variable.

The finding closes only when a later re-audit by the truthfulness auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

Doc-only fix: Define's exception doc now says a reference in the value names no defined variable.

## Log

- 2026-11-04: Created.
- 2026-11-04: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
- 2026-11-04: Doing -> Done. Define's exception doc corrected
