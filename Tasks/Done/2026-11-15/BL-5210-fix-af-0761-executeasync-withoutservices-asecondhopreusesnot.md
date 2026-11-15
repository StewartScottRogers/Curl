---
id: BL-5210
title: Fix AF-0761: ExecuteAsync_WithoutServices_ASecondHopReusesNothingAndStillSendsATraceParent checks only that some traceparent exists
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extension.OpenTelemetry.UnitTests]
requirement: none
created: 2026-11-14
completed: 2026-11-15
---
# BL-5210 — Fix AF-0761: ExecuteAsync_WithoutServices_ASecondHopReusesNothingAndStillSendsATraceParent checks only that some traceparent exists

## Goal

The defect the audit office reported as AF-0761 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0761 (Low, quality auditor), which Stewart accepted. The finding's file is on the audit office's paths, which a dark factory lane may not read or run; everything this task needs is below.

Location: `Curl.Extension.OpenTelemetry.UnitTests/OpenTelemetryRedirectTests.cs:105`

Location: `Curl.Extension.OpenTelemetry.UnitTests/OpenTelemetryRedirectTests.cs:105`

The only assertion is Assert.IsNotNull(TraceParentOf(sent[0])). It never checks the traceparent's format (00-<32 hex>-<16 hex>-<flags>) or anything about 'reuses nothing': the test runs a single hop, Hop(1), so there is nothing that could be reused. A malformed traceparent, or one reused from an earlier hop, would pass.

Reproduction, from the finding:

Run from the repository root:

```powershell
Select-String -Path Curl.Extension.OpenTelemetry.UnitTests/OpenTelemetryRedirectTests.cs -Pattern 'ASecondHopReusesNothing' -Context 0,8
```

- Expected: Assertions on the traceparent's exact shape and on what the second hop does not reuse.
- Actual: Line 105: Assert.IsNotNull(TraceParentOf(sent[0])); is the only check.

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

Test now asserts exact traceparent shape and a single header on two hops. Committed with the task.

## Log

- 2026-11-14: Created.
- 2026-11-15: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
- 2026-11-15: Doing -> Done. Test asserts traceparent shape on two hops
