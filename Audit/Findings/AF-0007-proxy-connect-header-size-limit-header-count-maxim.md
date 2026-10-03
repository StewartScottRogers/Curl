---
id: AF-0007
title: Proxy CONNECT header size limit `header.Count > MaximumHeaderBytes` can become >= with no test failing
auditor: quality
severity: Medium
status: accepted
reason: 
key: quality:Curl.Networking.UnitLibrary/HttpProxyTunnel.cs:ReadReply-header-limit-gt:surviving-mutant
task: BL-1263
found: 2026-10-02
found-at: 337ed10b42ddd4d09991deaecb10826c2dedba00
scorecard: 2026-10-02_1400.md
closed:
closed-by:
---
# AF-0007 - Proxy CONNECT header size limit `header.Count > MaximumHeaderBytes` can become >= with no test failing

## Summary

Medium finding from the quality auditor at `Curl.Networking.UnitLibrary/HttpProxyTunnel.cs:170`: Proxy CONNECT header size limit `header.Count > MaximumHeaderBytes` can become >= with no test failing. Reported by an auditor flagged unreliable in 2026-10-02_1400.md.

## Evidence

Location: `Curl.Networking.UnitLibrary/HttpProxyTunnel.cs:170`

Mutant survived (seed 0): `>` became `>=`. The failure message "Too large response headers: N > Max" is user-visible, yet no test sends a header exactly MaximumHeaderBytes long, so the boundary is unpinned.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Library Curl.Networking.UnitLibrary -MaxMutants 40 -Seed 0 -TimeoutSeconds 300 -OutFile $env:TEMP\mutation-net.json
```

- Expected: The mutant at Curl.Networking.UnitLibrary/HttpProxyTunnel.cs:170 (>) is killed.
- Actual: survived  Curl.Networking.UnitLibrary/HttpProxyTunnel.cs:170 >

## Re-audits

## Log

- 2026-10-02: filed proposed.
- 2026-10-02: proposed -> accepted.
