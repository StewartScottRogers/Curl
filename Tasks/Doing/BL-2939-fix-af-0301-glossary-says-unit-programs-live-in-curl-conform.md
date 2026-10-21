---
id: BL-2939
title: Fix AF-0301: Glossary says unit programs live in Curl.Conformance.UnitProgram.UnitLibrary and UnitPrograms.Ported; neither exists
priority: High
assignee: Claude
pipeline: docs
depends-on: []
touches: [Documentation]
requirement: none
created: 2026-10-21
completed:
---
# BL-2939 — Fix AF-0301: Glossary says unit programs live in Curl.Conformance.UnitProgram.UnitLibrary and UnitPrograms.Ported; neither exists

## Goal

The defect the audit office reported as AF-0301 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0301 (High, truthfulness auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0301-glossary-says-unit-programs-live-in-curl-conforman.md`.

Location: `Documentation/Wiki/Glossary.md:33`

Location: `Documentation/Wiki/Glossary.md:33`

Glossary line 33, 'Unit program', states as fact: 'It lives in `Curl.Conformance.UnitProgram.UnitLibrary`, is registered in `UnitPrograms.Ported`, and returns its failure count; a `unittest` case with no registered one is skipped (ADR-0711).' Its code-name column lists `IUnitProgram`, `UnitPrograms.Ported`. The tree has no directory Curl.Conformance.UnitProgram.UnitLibrary (or .UnitTests), and no .cs file declares IUnitProgram or UnitPrograms. The 'Curl has no port of unit1302' skip reason ADR-0711 describes is also absent. The same Glossary marks curl-config 'planned' at line 92, but writes this entry as if built. An agent following the canonical glossary would look for a project and types that do not exist. Phase 2: ADR-0711 (Accepted, 2026-10-19) decides this design, and its Consequences say 'BL-2841 builds the mechanism and the empty table', so the entry describes intent and should be written as intent.

Reproduction, from the finding:

Run from the repository root:

```powershell
Select-String -Path Documentation/Wiki/Glossary.md -Pattern 'It lives in .Curl.Conformance.UnitProgram.UnitLibrary.' | ForEach-Object { '{0}: glossary says it lives there' -f $_.LineNumber }; 'project exists: ' + (Test-Path Curl.Conformance.UnitProgram.UnitLibrary); 'IUnitProgram defined: ' + [bool](Get-ChildItem -Recurse -Filter *.cs | Where-Object { $_.FullName -notmatch '[\\/](bin|obj)[\\/]' } | Select-String -Pattern 'interface IUnitProgram' -List)
```

- Expected: Either the entry is written as planned (no match on 'It lives in'), or project exists: True and IUnitProgram defined: True.
- Actual: 33: glossary says it lives there
project exists: False
IUnitProgram defined: False

The finding closes only when a later re-audit by the truthfulness auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-21: Created.
- 2026-10-21: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
