---
id: AF-0761
title: ExecuteAsync_WithoutServices_ASecondHopReusesNothingAndStillSendsATraceParent checks only that some traceparent exists
auditor: quality
severity: Low
status: proposed
reason:
key: quality:Curl.Extension.OpenTelemetry.UnitTests/OpenTelemetryRedirectTests.cs:ExecuteAsync_WithoutServices_ASecondHopReusesNothingAndStillSendsATraceParent:weak-assertion
reproduction: none
task: none
tasks:
found: 2026-11-14
found-at: 92f6fc980babe08d1ebff5f02d7e076e42c67fd7
scorecard: 2026-11-14_1801.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0761 - ExecuteAsync_WithoutServices_ASecondHopReusesNothingAndStillSendsATraceParent checks only that some traceparent exists

## Summary

Low finding from the quality auditor at `Curl.Extension.OpenTelemetry.UnitTests/OpenTelemetryRedirectTests.cs:105`: ExecuteAsync_WithoutServices_ASecondHopReusesNothingAndStillSendsATraceParent checks only that some traceparent exists. Reported by an auditor flagged unreliable in 2026-11-14_1801.md.

## Evidence

Location: `Curl.Extension.OpenTelemetry.UnitTests/OpenTelemetryRedirectTests.cs:105`

The only assertion is Assert.IsNotNull(TraceParentOf(sent[0])). It never checks the traceparent's format (00-<32 hex>-<16 hex>-<flags>) or anything about 'reuses nothing': the test runs a single hop, Hop(1), so there is nothing that could be reused. A malformed traceparent, or one reused from an earlier hop, would pass.

## Reproduction

Run from the repository root:

```powershell
Select-String -Path Curl.Extension.OpenTelemetry.UnitTests/OpenTelemetryRedirectTests.cs -Pattern 'ASecondHopReusesNothing' -Context 0,8
```

- Expected: Assertions on the traceparent's exact shape and on what the second hop does not reuse.
- Actual: Line 105: Assert.IsNotNull(TraceParentOf(sent[0])); is the only check.

## Re-audits

## Log

- 2026-11-14: filed proposed.
