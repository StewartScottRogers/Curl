---
id: AF-0652
title: Surviving mutant: an MQTT publish with CURLOPT_POSTFIELDSIZE 0 can send every post field instead of none
auditor: quality
severity: High
status: closed
reason: Re-audit 2026-11-04_1442.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
key: quality:Curl.Engine.UnitLibrary/ManagedEasyTransfer.Context.cs:MqttPostDataOf-lt:surviving-mutant
reproduction: mutation Curl.Engine.UnitLibrary/ManagedEasyTransfer.Context.cs:241:<
task: BL-4834
tasks: BL-4834
found: 2026-11-04
found-at: 650d20c75108547e7bc74da2e86fbee9c49f7931
scorecard: 2026-11-04_0643.md
duplicate-of:
closed: 2026-11-04
closed-how: mechanical
closed-by: 2026-11-04_1442.md
---
# AF-0652 - Surviving mutant: an MQTT publish with CURLOPT_POSTFIELDSIZE 0 can send every post field instead of none

## Summary

High finding from the quality auditor at `Curl.Engine.UnitLibrary/ManagedEasyTransfer.Context.cs:241`: Surviving mutant: an MQTT publish with CURLOPT_POSTFIELDSIZE 0 can send every post field instead of none.

## Evidence

Location: `Curl.Engine.UnitLibrary/ManagedEasyTransfer.Context.cs:241`

Mutant 'size < 0' -> 'size <= 0' in 'return size < 0 || size >= fields.Length ? fields : fields[..(int)size];' survived. PostFieldSize defaults to -1. With size 0 the original sends an empty payload; the mutant sends the whole post-field bytes. That changes request bytes, and no Engine test notices.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Engine.UnitLibrary/ManagedEasyTransfer.Context.cs:241:< -Member MqttPostDataOf -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: killed
- Actual: survived

## Re-audits

- 2026-11-04 | 2026-11-04_1442.md | reproduces: no | Ran the -Site command: resolvedLine 241, mutated 'size <= 0 || ...', outcome killed. Baseline left out TransferContextFactoryTests.Create_CommandLineOptions_AreCopiedFromTheParsedCommandLine, which fails unmutated (filed separately). Runner's targeted mutation rerun on the clean audited commit: killed.

## Log

- 2026-11-04: filed proposed.
- 2026-11-04: proposed -> accepted.
- 2026-11-04: accepted -> closed. Re-audit 2026-11-04_1442.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
