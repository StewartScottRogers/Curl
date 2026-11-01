---
id: AF-0517
title: Of_TwentyKilobyteHeader_StillSends has no assertion; its field count goes only to the diagnostics log
auditor: quality
severity: Low
status: accepted
reason: Stewart accepted it on 2026-11-01 ("accept all").
key: quality:Curl.Original.Protocol.Http.UnitTests/Cgr271Tests.cs:Of_TwentyKilobyteHeader_StillSends:no-assertion
reproduction: none
task: BL-4446
tasks: BL-4446
found: 2026-11-01
found-at: 8dd012a88d26bb19a4e7d95f0be5a3371e3d2b15
scorecard: 2026-11-01_0144.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0517 - Of_TwentyKilobyteHeader_StillSends has no assertion; its field count goes only to the diagnostics log

## Summary

Low finding from the quality auditor at `Curl.Original.Protocol.Http.UnitTests/Cgr271Tests.cs:35`: Of_TwentyKilobyteHeader_StillSends has no assertion; its field count goes only to the diagnostics log.

## Evidence

Location: `Curl.Original.Protocol.Http.UnitTests/Cgr271Tests.cs:35`

The test's only check is Diagnostics.Assert("field count", 5, Http2RequestHeaders.Of(...).Count) at line 35, which logs and cannot fail (TestDiagnostics.cs:189-193). Any field count passes; only an exception would fail it.

## Reproduction

Run from the repository root:

```powershell
Select-String -Path Curl.Original.Protocol.Http.UnitTests/Cgr271Tests.cs -Pattern 'Of_TwentyKilobyteHeader_StillSends' -Context 0,6
```

- Expected: The body ends in Assert.AreEqual(5, ...Count) or equivalent.
- Actual: The body ends in Diagnostics.Assert("field count", 5, ...Count); no Assert.* call.

## Re-audits

## Log

- 2026-11-01: filed proposed.
- 2026-11-01: proposed -> accepted. Stewart: accept all.
