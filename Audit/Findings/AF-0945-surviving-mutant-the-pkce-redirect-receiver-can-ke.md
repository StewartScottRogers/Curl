---
id: AF-0945
title: Surviving mutant: the PKCE redirect receiver can keep reading after the request head ends
auditor: quality
severity: Medium
status: accepted
reason: Accepted under Stewart's standing accept of 2026-11-02.
key: quality:Curl.Extension.OAuth2.UnitLibrary/Flows/OAuth2RedirectReceiver.cs:ReadRequestLineAsync-and:surviving-mutant
reproduction: mutation Curl.Extension.OAuth2.UnitLibrary/Flows/OAuth2RedirectReceiver.cs:45:&&
task: BL-5704
tasks: BL-5704
found: 2026-11-21
found-at: 57bece6c118995fac41401b81793a8395d3fb16d
scorecard: 2026-11-21_0835.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0945 - Surviving mutant: the PKCE redirect receiver can keep reading after the request head ends

## Summary

Medium finding from the quality auditor at `Curl.Extension.OAuth2.UnitLibrary/Flows/OAuth2RedirectReceiver.cs:45`: Surviving mutant: the PKCE redirect receiver can keep reading after the request head ends. Reported by an auditor flagged unreliable in 2026-11-21_0835.md.

## Evidence

Location: `Curl.Extension.OAuth2.UnitLibrary/Flows/OAuth2RedirectReceiver.cs:45`

Drawn library, seed 812510: while (filled < buffer.Length && ...IndexOf(CRLFCRLF) < 0) mutated to || survived. The mutant keeps reading after the blank line until the buffer is full; a browser waiting for the answer on an open connection leaves the flow hanging until its timeout. The tests' scripted connections end the stream, so the extra read returns 0.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Extension.OAuth2.UnitLibrary/Flows/OAuth2RedirectReceiver.cs:45:&& -Member ReadRequestLineAsync -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

## Re-audits

## Log

- 2026-11-21: filed proposed.
