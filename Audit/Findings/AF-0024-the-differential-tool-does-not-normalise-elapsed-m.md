---
id: AF-0024
title: The differential tool does not normalise elapsed milliseconds in error text or the -v source port, so identical behaviour is counted as a difference
auditor: conformance
severity: Low
status: accepted
reason: 
key: conformance:Audit/Tools/Invoke-DifferentialConformance.ps1:Get-Normalised:stderr
reproduction: none
task: BL-1280
tasks: BL-1280
found: 2026-10-02
found-at: 337ed10b42ddd4d09991deaecb10826c2dedba00
scorecard: 2026-10-02_1400.md
duplicate-of:
closed:
closed-how:
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

- 2026-10-03 | 2026-10-03_0623.md | reproduces: yes | Ran the reproduction: the count is 0. Invoke-DifferentialConformance.ps1's Get-Normalised still normalises only the port, Date, boundary, meter lines and the PROXY source port. In this audit's run (seed 1162681642), 4 of the 5 reported differences are this gap alone. Cases 152, 179 and 254 differ only in 'after 0 ms' (curl) vs 'after 7/6/42 ms' (Curl) in an otherwise identical exit-7 message. Case 157 differs only in the -v 'Established connection ... from 127.0.0.1 port 55548/55549' source port.
- 2026-10-07 | 2026-10-07_0844.md | reproduces: yes | Ran the reproduction: count 0. Invoke-DifferentialConformance.ps1 still has no normalisation for 'after N ms' or the -v 'Established ... port N' source port. In this audit's run (seed 1516403247) 8 of the 14 differing cases are only that noise: 35, 71, 79, 137, 202, 223 and 227 differ only in 'after N ms', and 154 (--get -v) differs only in 'from 127.0.0.1 port 51113' versus '51114'.

## Log

- 2026-10-02: filed proposed.
- 2026-10-02: proposed -> accepted.
