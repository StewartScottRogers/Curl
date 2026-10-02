---
id: AF-0008
title: ConnectToMappings.Failure can report IsMapped: true instead of false with no test failing
auditor: quality
severity: Medium
status: proposed
reason:
key: quality:Curl.Networking.UnitLibrary/ConnectToMappings.cs:Failure-IsMapped:surviving-mutant
task: none
found: 2026-10-02
found-at: 337ed10b42ddd4d09991deaecb10826c2dedba00
scorecard: 2026-10-02_1400.md
closed:
closed-by:
---
# AF-0008 - ConnectToMappings.Failure can report IsMapped: true instead of false with no test failing

## Summary

Medium finding from the quality auditor at `Curl.Networking.UnitLibrary/ConnectToMappings.cs:135`: ConnectToMappings.Failure can report IsMapped: true instead of false with no test failing.

## Evidence

Location: `Curl.Networking.UnitLibrary/ConnectToMappings.cs:135`

Mutant survived (seed 0): `new(string.Empty, 0, IsMapped: false, message)` became `IsMapped: true`. A failed --connect-to parse could then claim a mapping, and no test checks IsMapped on a failure result.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Library Curl.Networking.UnitLibrary -MaxMutants 40 -Seed 0 -TimeoutSeconds 300 -OutFile $env:TEMP\mutation-net.json
```

- Expected: The mutant at Curl.Networking.UnitLibrary/ConnectToMappings.cs:135 (false) is killed.
- Actual: survived  Curl.Networking.UnitLibrary/ConnectToMappings.cs:135 false

## Re-audits

## Log

- 2026-10-02: filed proposed.
