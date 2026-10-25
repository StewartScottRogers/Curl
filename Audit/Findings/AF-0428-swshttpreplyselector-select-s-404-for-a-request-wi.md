---
id: AF-0428
title: SwsHttpReplySelector.Select's 404 for a request without a path can stop closing the connection with no test failing
auditor: quality
severity: Medium
status: closed
reason: Re-audit 2026-10-25_0941.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
key: quality:Curl.Conformance.UnitLibrary/SwsHttpReplySelector.cs:Select-true:surviving-mutant
reproduction: mutation Curl.Conformance.UnitLibrary/SwsHttpReplySelector.cs:77:true
task: BL-3494
tasks: BL-3494
found: 2026-10-24
found-at: 456765a13655caedad49326c988f9e4eec86da8b
scorecard: 2026-10-24_2106.md
duplicate-of:
closed: 2026-10-25
closed-how: mechanical
closed-by: 2026-10-25_0941.md
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

- 2026-10-25 | 2026-10-25_0941.md | reproduces: no | Ran the -Site reproduction (SwsHttpReplySelector.cs:77:true, -Member Select): resolvedLine 77, 'new SwsHttpReply(..., true, false)' -> '(..., false, false)', outcome killed. Runner's targeted mutation rerun on the clean audited commit: killed.

## Log

- 2026-10-24: filed proposed.
- 2026-10-25: proposed -> accepted.
- 2026-10-25: accepted -> closed. Re-audit 2026-10-25_0941.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
