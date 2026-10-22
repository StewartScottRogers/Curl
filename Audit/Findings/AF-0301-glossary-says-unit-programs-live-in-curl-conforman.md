---
id: AF-0301
title: Glossary says unit programs live in Curl.Conformance.UnitProgram.UnitLibrary and UnitPrograms.Ported; neither exists
auditor: truthfulness
severity: High
status: closed
reason: Re-audit 2026-10-22_1120.md: the reproduction no longer reproduces.
key: truthfulness:Documentation/Wiki/Glossary.md:Unit-program:false-statement
reproduction: none
task: BL-2939
tasks: BL-2939
found: 2026-10-19
found-at: 1b3c70b1436b2d499099c5c6dd7bb119e11f5631
scorecard: 2026-10-19_2134.md
duplicate-of:
closed: 2026-10-22
closed-how: reliable-reaudit
closed-by: 2026-10-22_1120.md
---
# AF-0301 - Glossary says unit programs live in Curl.Conformance.UnitProgram.UnitLibrary and UnitPrograms.Ported; neither exists

## Summary

High finding from the truthfulness auditor at `Documentation/Wiki/Glossary.md:33`: Glossary says unit programs live in Curl.Conformance.UnitProgram.UnitLibrary and UnitPrograms.Ported; neither exists.

## Evidence

Location: `Documentation/Wiki/Glossary.md:33`

Glossary line 33, 'Unit program', states as fact: 'It lives in `Curl.Conformance.UnitProgram.UnitLibrary`, is registered in `UnitPrograms.Ported`, and returns its failure count; a `unittest` case with no registered one is skipped (ADR-0711).' Its code-name column lists `IUnitProgram`, `UnitPrograms.Ported`. The tree has no directory Curl.Conformance.UnitProgram.UnitLibrary (or .UnitTests), and no .cs file declares IUnitProgram or UnitPrograms. The 'Curl has no port of unit1302' skip reason ADR-0711 describes is also absent. The same Glossary marks curl-config 'planned' at line 92, but writes this entry as if built. An agent following the canonical glossary would look for a project and types that do not exist. Phase 2: ADR-0711 (Accepted, 2026-10-19) decides this design, and its Consequences say 'BL-2841 builds the mechanism and the empty table', so the entry describes intent and should be written as intent.

## Reproduction

Run from the repository root:

```powershell
Select-String -Path Documentation/Wiki/Glossary.md -Pattern 'It lives in .Curl.Conformance.UnitProgram.UnitLibrary.' | ForEach-Object { '{0}: glossary says it lives there' -f $_.LineNumber }; 'project exists: ' + (Test-Path Curl.Conformance.UnitProgram.UnitLibrary); 'IUnitProgram defined: ' + [bool](Get-ChildItem -Recurse -Filter *.cs | Where-Object { $_.FullName -notmatch '[\\/](bin|obj)[\\/]' } | Select-String -Pattern 'interface IUnitProgram' -List)
```

- Expected: Either the entry is written as planned (no match on 'It lives in'), or project exists: True and IUnitProgram defined: True.
- Actual: 33: glossary says it lives there
project exists: False
IUnitProgram defined: False

## Re-audits

- 2026-10-22 | 2026-10-22_0130.md | reproduces: no | Ran the reproduction: '33: glossary says it lives there', 'project exists: True', 'IUnitProgram defined: True'. Curl.Conformance.UnitProgram.UnitLibrary exists, and UnitPrograms.Ported is defined at Curl.Conformance.UnitProgram.UnitLibrary/UnitPrograms.cs:34, so the glossary's statement is now true.
- 2026-10-22 | 2026-10-22_1120.md | reproduces: no | Ran the reproduction: the glossary (line 33) still says unit programs live in Curl.Conformance.UnitProgram.UnitLibrary, and that is now true: 'project exists: True', 'IUnitProgram defined: True', and UnitPrograms.Ported is defined in that project.

## Log

- 2026-10-19: filed proposed.
- 2026-10-21: proposed -> accepted.
- 2026-10-22: accepted -> closed. Re-audit 2026-10-22_1120.md: the reproduction no longer reproduces.
