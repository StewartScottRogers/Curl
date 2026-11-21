---
id: AF-0925
title: Surviving mutant: the DER reader can refuse a source of exactly curl's maximum length
auditor: quality
severity: High
status: accepted
reason: Accepted under Stewart's standing accept of 2026-11-02.
key: quality:Curl.Output.UnitLibrary/DerReader.cs:HasReadableIdentifier-gt:surviving-mutant
reproduction: mutation Curl.Output.UnitLibrary/DerReader.cs:77:>
task: BL-5675
tasks: BL-5675
found: 2026-11-21
found-at: 1f330a1f28a1086717db7833947030e8fdc0bf9d
scorecard: 2026-11-21_0114.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0925 - Surviving mutant: the DER reader can refuse a source of exactly curl's maximum length

## Summary

High finding from the quality auditor at `Curl.Output.UnitLibrary/DerReader.cs:77`: Surviving mutant: the DER reader can refuse a source of exactly curl's maximum length.

## Evidence

Location: `Curl.Output.UnitLibrary/DerReader.cs:77`

Mutant 'end - start > MaximumSourceLength' -> 'end - start >= MaximumSourceLength' survived (seed 276683 sample). MaximumSourceLength is 0x40000 (line 20). The mutant refuses to read a DER element spanning exactly that many bytes, which changes the certificate text the verbose and certinfo output prints. No test reads a source at the boundary.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Output.UnitLibrary/DerReader.cs:77:> -Member HasReadableIdentifier -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

## Re-audits

## Log

- 2026-11-21: filed proposed.
