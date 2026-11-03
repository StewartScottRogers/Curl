---
id: AF-0535
title: Surviving mutant: UrlHandleParts.WrittenPortOf can drop an explicit port 0 without a test failing
auditor: quality
severity: Medium
status: proposed
reason:
key: quality:Curl.Engine.UnitLibrary/UrlHandleParts.cs:WrittenPortOf-lt:surviving-mutant
reproduction: mutation Curl.Engine.UnitLibrary/UrlHandleParts.cs:132:<
task: none
tasks:
found: 2026-11-02
found-at: fcdba3d03e8b5378439deb78f2055b6f5eccf2bb
scorecard: 2026-11-02_1724.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0535 - Surviving mutant: UrlHandleParts.WrittenPortOf can drop an explicit port 0 without a test failing

## Summary

Medium finding from the quality auditor at `Curl.Engine.UnitLibrary/UrlHandleParts.cs:132`: Surviving mutant: UrlHandleParts.WrittenPortOf can drop an explicit port 0 without a test failing. Reported by an auditor flagged unreliable in 2026-11-02_1724.md.

## Evidence

Location: `Curl.Engine.UnitLibrary/UrlHandleParts.cs:132`

Mutant 'if (url.Port < 0)' -> 'if (url.Port <= 0)' survived (Curl.Engine.UnitTests green). A URL written with port 0 (http://h:0/) would report no stored port, so curl_url_get(CURLUPART_PORT) answers CURLUE_NO_PORT and the rebuilt URL loses ':0' where upstream keeps the written port. No test covers port 0 in the URL handle.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Engine.UnitLibrary/UrlHandleParts.cs:132:< -Member WrittenPortOf -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: killed
- Actual: survived

## Re-audits

## Log

- 2026-11-02: filed proposed.
