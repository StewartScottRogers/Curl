---
id: AF-0643
title: HttpFileVariables.Define documents a throw when the value 'references no variable'; it throws only for a reference to an undefined variable
auditor: truthfulness
severity: Low
status: proposed
reason:
key: truthfulness:Curl.Extension.HttpFile.UnitLibrary/HttpFileVariables.cs:Define:false-doc-comment
reproduction: none
task: none
tasks:
found: 2026-11-03
found-at: 0ae5ce3f5e65976edeb9facb28b93fd3f4dcdbc8
scorecard: 2026-11-03_2203.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0643 - HttpFileVariables.Define documents a throw when the value 'references no variable'; it throws only for a reference to an undefined variable

## Summary

Low finding from the truthfulness auditor at `Curl.Extension.HttpFile.UnitLibrary/HttpFileVariables.cs:30`: HttpFileVariables.Define documents a throw when the value 'references no variable'; it throws only for a reference to an undefined variable.

## Evidence

Location: `Curl.Extension.HttpFile.UnitLibrary/HttpFileVariables.cs:30`

Define's doc: '<exception cref="ArgumentExpansionException">The value references no variable (exit 2).</exception>'. That reads as a throw for a value with no {{reference}}. The code is Define => values[name] = Substitute(value). Substitute appends a value with no {{...}} unchanged, and ValueOf throws only for a reference whose name is not in values ('http-file: variable ... is not defined', CurlExitCode.FailedInit). Substitute's own doc says it correctly: 'A reference names no variable (exit 2).' Sampled method (seed 221035).

## Reproduction

Run from the repository root:

```powershell
Select-String -Path Curl.Extension.HttpFile.UnitLibrary/HttpFileVariables.cs -Pattern 'references no variable|names no variable|is not defined'
```

- Expected: Define's exception doc says the throw is for a reference that names no defined variable, as Substitute's does.
- Actual: Line 30 says 'The value references no variable (exit 2)', while the throw (line ~68) is for an undefined referenced variable.

## Re-audits

## Log

- 2026-11-03: filed proposed.
