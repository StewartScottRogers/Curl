---
id: AF-0024
title: The differential tool does not normalise elapsed milliseconds in error text or the -v source port, so identical behaviour is counted as a difference
auditor: conformance
severity: Low
status: accepted
reason: 
key: conformance:Audit/Tools/Invoke-DifferentialConformance.ps1:Get-Normalised:stderr
task: none
found: 2026-10-02
found-at: 337ed10b42ddd4d09991deaecb10826c2dedba00
scorecard: 2026-10-02_1400.md
closed:
closed-by:
---
# AF-0024 - The differential tool does not normalise elapsed milliseconds in error text or the -v source port, so identical behaviour is counted as a difference

## Summary

Low finding from the conformance auditor at `Audit/Tools/Invoke-DifferentialConformance.ps1:230`: The differential tool does not normalise elapsed milliseconds in error text or the -v source port, so identical behaviour is counted as a difference.

## Evidence

Location: `Audit/Tools/Invoke-DifferentialConformance.ps1:230`

Seed 863948043, reference curl 8.21.0 (x86_64-w64-mingw32) Schannel. Cases 95, 116, 159, 187, 223, 282, 284 differ only in 'Failed to connect to 127.0.0.1:PORT after N ms' (for example 2730 ms vs 2724 ms; 0 ms vs 8 ms). Case 254 differs only in '* Established connection ... from 127.0.0.1 port N'. Case 299 differs only in where a progress-meter row lands relative to a warning, which also varies between two runs of real curl. Get-Normalised (line 230) masks the server port, Date, multipart boundary, HAProxy source port and meter lines, but not these values. So 9 of the 43 reported differences (21%) are run-to-run noise, which inflates differentialDifferences.

## Reproduction

Run from the repository root:

```powershell
Select-String -Path Audit/Tools/Invoke-DifferentialConformance.ps1 -Pattern 'after \\d\+ ms|port \\d|Established' | Measure-Object | Select-Object -ExpandProperty Count
```

- Expected: At least 1: Get-Normalised masks 'after N ms' and the verbose source port
- Actual: 0

## Re-audits

## Log

- 2026-10-02: filed proposed.
- 2026-10-02: proposed -> accepted.
