---
id: AF-0428
title: SwsHttpReplySelector.Select's 404 for a request without a path can stop closing the connection with no test failing
auditor: quality
severity: Medium
status: proposed
reason:
key: quality:Curl.Conformance.UnitLibrary/SwsHttpReplySelector.cs:Select-true:surviving-mutant
reproduction: mutation Curl.Conformance.UnitLibrary/SwsHttpReplySelector.cs:77:true
task: none
tasks:
found: 2026-10-24
found-at: 456765a13655caedad49326c988f9e4eec86da8b
scorecard: 2026-10-24_2106.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0428 - SwsHttpReplySelector.Select's 404 for a request without a path can stop closing the connection with no test failing

## Summary

Medium finding from the quality auditor at `Curl.Conformance.UnitLibrary/SwsHttpReplySelector.cs:77`: SwsHttpReplySelector.Select's 404 for a request without a path can stop closing the connection with no test failing.

## Evidence

Location: `Curl.Conformance.UnitLibrary/SwsHttpReplySelector.cs:77`

Sampled mutant (seed 0) survived: 'return new SwsHttpReply(Encoding.Latin1.GetBytes(NotFoundDocument), true, false);' -> '... false, false);'. SwsHttpReply's second parameter is closesConnection (SwsHttpReply.cs:4). With the mutant, the sws stand-in keeps a connection open after answering an unparseable request line with 404, where upstream's sws closes it. This decides what the conformance harness serves, so a ratchet case could pass or hang differently than against the real server, and no test notices.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Conformance.UnitLibrary/SwsHttpReplySelector.cs:77:true -Member Select -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

## Re-audits

## Log

- 2026-10-24: filed proposed.
