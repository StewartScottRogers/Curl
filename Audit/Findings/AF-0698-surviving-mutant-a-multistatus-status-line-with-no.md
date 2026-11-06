---
id: AF-0698
title: Surviving mutant: a multistatus status line with no reason phrase can be refused
auditor: quality
severity: Medium
status: proposed
reason:
key: quality:Curl.Extension.WebDavTools.UnitLibrary/Multistatus/WebDavMultistatusReader.cs:ParseStatus-lt:surviving-mutant
reproduction: mutation Curl.Extension.WebDavTools.UnitLibrary/Multistatus/WebDavMultistatusReader.cs:101:<
task: none
tasks:
found: 2026-11-05
found-at: 4eba3c63638befcc6a63aaa31ddf5419922fa939
scorecard: 2026-11-05_2025.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0698 - Surviving mutant: a multistatus status line with no reason phrase can be refused

## Summary

Medium finding from the quality auditor at `Curl.Extension.WebDavTools.UnitLibrary/Multistatus/WebDavMultistatusReader.cs:101`: Surviving mutant: a multistatus status line with no reason phrase can be refused.

## Evidence

Location: `Curl.Extension.WebDavTools.UnitLibrary/Multistatus/WebDavMultistatusReader.cs:101`

'if (parts.Length < 2' mutated to '<= 2' survived. A valid status like 'HTTP/1.1 200' (no reason phrase) splits into 2 parts and would throw InvalidStatus. Every test status carries a reason phrase, and the refused cases ('HTTP/1.1', 'FTP/1.1 200 OK', ...) do not cover two parts.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Extension.WebDavTools.UnitLibrary/Multistatus/WebDavMultistatusReader.cs:101:< -Member ParseStatus -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: killed
- Actual: survived

## Re-audits

## Log

- 2026-11-05: filed proposed.
