---
id: BL-3123
title: Fix AF-0343: DnsServerList.TryParsePortSuffix's given > IPEndPoint.MaxPort can become >= with no test failing, so --dns-servers host:65535 is refused
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary]
requirement: none
created: 2026-10-22
completed:
---
# BL-3123 — Fix AF-0343: DnsServerList.TryParsePortSuffix's given > IPEndPoint.MaxPort can become >= with no test failing, so --dns-servers host:65535 is refused

## Goal

The defect the audit office reported as AF-0343 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0343 (High, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0343-dnsserverlist-tryparseportsuffix-s-given-ipendpoin.md`.

Location: `Curl.Networking.UnitLibrary/DnsServerList.cs:113`

Location: `Curl.Networking.UnitLibrary/DnsServerList.cs:113`

Sampled mutant (seed 0) survived: 'given > IPEndPoint.MaxPort' became 'given >= IPEndPoint.MaxPort'. A --dns-servers entry naming port 65535 is valid, but the mutant refuses it, failing the option with an exit code instead of resolving. No Curl.Networking.UnitTests test pins the top port boundary.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Networking.UnitLibrary/DnsServerList.cs:113:> -Member TryParsePortSuffix -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: killed
- Actual: survived

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-22: Created.
