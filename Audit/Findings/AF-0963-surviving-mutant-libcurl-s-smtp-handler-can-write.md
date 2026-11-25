---
id: AF-0963
title: Surviving mutant: libcurl's SMTP handler can write --trace-config smtp state lines by default (tracesSmtp = false -> true)
auditor: quality
severity: High
status: closed
reason: Re-audit 2026-11-22_1351.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
key: quality:Curl.Engine.UnitLibrary/EngineComposition.cs:CreateProtocolHandlers-false:surviving-mutant
reproduction: mutation Curl.Engine.UnitLibrary/EngineComposition.cs:146:false
task: BL-5734
tasks: BL-5734
found: 2026-11-22
found-at: 9f9f37761162551e34c06d99ea4c7ebfb0168e83
scorecard: 2026-11-22_0210.md
duplicate-of:
closed: 2026-11-22
closed-how: mechanical
closed-by: 2026-11-22_1351.md
---
# AF-0963 - Surviving mutant: libcurl's SMTP handler can write --trace-config smtp state lines by default (tracesSmtp = false -> true)

## Summary

High finding from the quality auditor at `Curl.Engine.UnitLibrary/EngineComposition.cs:146`: Surviving mutant: libcurl's SMTP handler can write --trace-config smtp state lines by default (tracesSmtp = false -> true). Reappeared; previously AF-0717.

## Evidence

Location: `Curl.Engine.UnitLibrary/EngineComposition.cs:146`

Seeded sample (seed 51850, -MaxMutants 40): the mutant at EngineComposition.cs:146 operator 'false' survived. Original: 'bool tracesSmtp = false,'; mutated: 'bool tracesSmtp = true,'. The value feeds 'new SmtpProtocolHandler(...) { TracesStateMachine = tracesSmtp }' (line 196). ManagedCurlEngine.CreateDispatcher (ManagedCurlEngine.cs:261) and CurlComposition.cs:1015 call CreateProtocolHandlers without tracesSmtp, so they rely on the default. With the mutant, every libcurl SMTP transfer under verbose or a debug callback writes '* [SMTP] state change ...' lines that curl writes only under --trace-config smtp. No Engine test pins the SMTP verbose lines of a transfer made without the trace setting. This changes output bytes.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Engine.UnitLibrary/EngineComposition.cs:146:false -Member CreateProtocolHandlers -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed: a test pins an SMTP transfer's verbose lines without --trace-config smtp
- Actual: outcome survived

## Re-audits

- 2026-11-22 | 2026-11-22_1351.md | reproduces: no | Ran the -Site command (3 Engine tests that already fail left out): 'bool tracesSmtp = false' -> 'true' at EngineComposition.cs:146 was killed. Runner's targeted mutation rerun on the clean audited commit: killed.

## Log

- 2026-11-22: filed proposed.
- 2026-11-22: accepted -> closed. Re-audit 2026-11-22_1351.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
