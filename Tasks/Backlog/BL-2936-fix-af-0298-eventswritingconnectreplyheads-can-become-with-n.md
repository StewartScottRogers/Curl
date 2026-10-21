---
id: BL-2936
title: Fix AF-0298: EventsWritingConnectReplyHeads' && can become || with no test failing, wrapping a null header output for every tunnel without -i/-D
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitLibrary]
requirement: none
created: 2026-10-21
completed:
---
# BL-2936 — Fix AF-0298: EventsWritingConnectReplyHeads' && can become || with no test failing, wrapping a null header output for every tunnel without -i/-D

## Goal

The defect the audit office reported as AF-0298 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0298 (High, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0298-eventswritingconnectreplyheads-can-become-with-no.md`.

Location: `Curl.Engine.UnitLibrary/TransferContextFactory.cs:592`

Location: `Curl.Engine.UnitLibrary/TransferContextFactory.cs:592`

Sampled mutant (seed 0) survived: IsHttpOrHttpsProxy(proxy) && headerOutput is not null && !proxyOptions.SuppressConnectHeaders => IsHttpOrHttpsProxy(proxy) && headerOutput is not null || !proxyOptions.SuppressConnectHeaders. With the default SuppressConnectHeaders = false, the condition is now always true. So a transfer through an HTTP(S) proxy tunnel with no header output (no -i, -I or -D, the common case) gets ConnectReplyHeadWritingEvents(events, null), whose WriteConnectReplyHeadAsync calls headerOutput.WriteAsync (ConnectReplyHeadWritingEvents.cs:26): a NullReferenceException on the CONNECT reply, so the exit code and output change. No test runs the context factory over a proxy tunnel without header output.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Engine.UnitLibrary/TransferContextFactory.cs:592:&& -Member EventsWritingConnectReplyHeads -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-21: Created.
