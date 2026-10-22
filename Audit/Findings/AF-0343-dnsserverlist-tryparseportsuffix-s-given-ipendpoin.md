---
id: AF-0343
title: DnsServerList.TryParsePortSuffix's given > IPEndPoint.MaxPort can become >= with no test failing, so --dns-servers host:65535 is refused
auditor: quality
severity: High
status: accepted
reason: 
key: quality:Curl.Networking.UnitLibrary/DnsServerList.cs:TryParsePortSuffix-gt:surviving-mutant
reproduction: mutation Curl.Networking.UnitLibrary/DnsServerList.cs:113:>
task: BL-3123
tasks: BL-3123
found: 2026-10-22
found-at: 608a755586679cf34031a0e5b137f01b9d7fb329
scorecard: 2026-10-22_1120.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0343 - DnsServerList.TryParsePortSuffix's given > IPEndPoint.MaxPort can become >= with no test failing, so --dns-servers host:65535 is refused

## Summary

High finding from the quality auditor at `Curl.Networking.UnitLibrary/DnsServerList.cs:113`: DnsServerList.TryParsePortSuffix's given > IPEndPoint.MaxPort can become >= with no test failing, so --dns-servers host:65535 is refused.

## Evidence

Location: `Curl.Networking.UnitLibrary/DnsServerList.cs:113`

Sampled mutant (seed 0) survived: 'given > IPEndPoint.MaxPort' became 'given >= IPEndPoint.MaxPort'. A --dns-servers entry naming port 65535 is valid, but the mutant refuses it, failing the option with an exit code instead of resolving. No Curl.Networking.UnitTests test pins the top port boundary.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Networking.UnitLibrary/DnsServerList.cs:113:> -Member TryParsePortSuffix -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: killed
- Actual: survived

## Re-audits

## Log

- 2026-10-22: filed proposed.
- 2026-10-22: proposed -> accepted.
