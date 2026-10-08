---
id: AF-0078
title: HttpConnectionPersistence.KeepsHttp10Alive: the close check's slice 'colon + 1' can become 'colon - 1' with no test failing
auditor: quality
severity: High
status: proposed
reason:
key: quality:Curl.Protocol.Http.UnitLibrary/HttpConnectionPersistence.cs:KeepsHttp10Alive-plus1:surviving-mutant
reproduction: mutation Curl.Protocol.Http.UnitLibrary/HttpConnectionPersistence.cs:89:+1
task: none
tasks:
found: 2026-10-08
found-at: 043959c94f40aaf4a1c37e70d0c6d957c5f1e564
scorecard: 2026-10-08_0748.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0078 - HttpConnectionPersistence.KeepsHttp10Alive: the close check's slice 'colon + 1' can become 'colon - 1' with no test failing

## Summary

High finding from the quality auditor at `Curl.Protocol.Http.UnitLibrary/HttpConnectionPersistence.cs:89`: HttpConnectionPersistence.KeepsHttp10Alive: the close check's slice 'colon + 1' can become 'colon - 1' with no test failing.

## Evidence

Location: `Curl.Protocol.Http.UnitLibrary/HttpConnectionPersistence.cs:89`

Sampled mutant (seed 0) survived: '&& !NamesOption(headerLine[(colon + 1)..], "close");' -> '&& !NamesOption(headerLine[(colon - 1)..], "close");'. NamesOption splits on ',' and trims, so the mutant reads the first token as 'n: close' and misses a close given first. For the HTTP/1.0 header 'Connection: close, keep-alive', curl's rule (keep-alive named and close not named) gives false, but the mutant gives true. HttpResponseHeadReader.cs:513 would then print the HTTP/1.0 keep-alive info line and keep a connection the server said to close. KeepsHttp10Alive_HeaderLine_IsTrueForAConnectionHeaderNamingKeepAliveAndNotClose (HttpConnectionPersistenceTests.cs:110-119) has 'keep-alive, close' but never 'close, keep-alive', so no row catches it.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Protocol.Http.UnitLibrary/HttpConnectionPersistence.cs:89:+1 -Member KeepsHttp10Alive -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

## Re-audits

## Log

- 2026-10-08: filed proposed.
