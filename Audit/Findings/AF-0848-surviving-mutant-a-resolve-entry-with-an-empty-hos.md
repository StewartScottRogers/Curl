---
id: AF-0848
title: Surviving mutant: a --resolve entry with an empty host is kept instead of ignored
auditor: quality
severity: High
status: closed
reason: Re-audit 2026-11-18_0810.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
key: quality:Curl.Networking.UnitLibrary/ResolveOverrides.cs:TryParseAddition-gt:surviving-mutant
reproduction: mutation Curl.Networking.UnitLibrary/ResolveOverrides.cs:150:>
task: BL-5368
tasks: BL-5368
found: 2026-11-17
found-at: 10c240a4859c7868ec16b722cde6234f80cc8fcb
scorecard: 2026-11-17_0430.md
duplicate-of:
closed: 2026-11-18
closed-how: mechanical
closed-by: 2026-11-18_0810.md
---
# AF-0848 - Surviving mutant: a --resolve entry with an empty host is kept instead of ignored

## Summary

High finding from the quality auditor at `Curl.Networking.UnitLibrary/ResolveOverrides.cs:150`: Surviving mutant: a --resolve entry with an empty host is kept instead of ignored.

## Evidence

Location: `Curl.Networking.UnitLibrary/ResolveOverrides.cs:150`

if (host.Length > 0) -> >= 0 survived. The code's own comment says ':80:127.0.0.1' is ignored (measured). Under the mutant the entry is added, so LoadResolveEntries writes an 'Added :80:127.0.0.1 to DNS cache' -v line curl does not write (stderr bytes).

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Networking.UnitLibrary/ResolveOverrides.cs:150:> -Member TryParseAddition -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: killed
- Actual: survived

## Re-audits

- 2026-11-18 | 2026-11-18_0810.md | reproduces: no | Ran the -Site command: ResolveOverrides.cs:150:> [TryParseAddition] 'host.Length > 0' -> '>= 0' resolvedLine 150, outcome killed. Runner's targeted mutation rerun on the clean audited commit: killed.

## Log

- 2026-11-17: filed proposed.
- 2026-11-18: accepted -> closed. Re-audit 2026-11-18_0810.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
