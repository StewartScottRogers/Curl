---
id: AF-0821
title: Surviving mutant: lib571 can report a corrupt final full-size RTP chunk with the wrong message
auditor: quality
severity: Medium
status: accepted
reason: Accepted under Stewart's standing accept of 2026-11-02.
key: quality:Curl.Conformance.LibTest.UnitLibrary/Lib571.cs:CheckPayload-gt:surviving-mutant
reproduction: mutation Curl.Conformance.LibTest.UnitLibrary/Lib571.cs:148:>
task: none
tasks:
found: 2026-11-16
found-at: 74f3f45c47e86300b2b262dc1dacd43b508b09aa
scorecard: 2026-11-16_0514.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0821 - Surviving mutant: lib571 can report a corrupt final full-size RTP chunk with the wrong message

## Summary

Medium finding from the quality auditor at `Curl.Conformance.LibTest.UnitLibrary/Lib571.cs:148`: Surviving mutant: lib571 can report a corrupt final full-size RTP chunk with the wrong message.

## Evidence

Location: `Curl.Conformance.LibTest.UnitLibrary/Lib571.cs:148`

`if (remaining > RtpData.Length)` (upstream lib571.c's `message_size - i > RTP_DATA_SIZE`) changed to `>=` survived. A corrupted last chunk of exactly RTP_DATA_SIZE bytes then prints 'RTP PAYLOAD CORRUPTED' instead of 'RTP PAYLOAD END CORRUPTED (n)', changing the port's output. No test corrupts a final full-size chunk.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Conformance.LibTest.UnitLibrary/Lib571.cs:148:> -Member CheckPayload -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: killed
- Actual: survived

## Re-audits

## Log

- 2026-11-16: filed proposed.
