---
id: BL-4735
title: Fix AF-0571: Received_MessageLargerThan64KiB checks the CONNECT only for null, not that it announces no Maximum Packet Size
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.Extended.UnitTests]
requirement: none
created: 2026-11-03
completed:
---
# BL-4735 — Fix AF-0571: Received_MessageLargerThan64KiB checks the CONNECT only for null, not that it announces no Maximum Packet Size

## Goal

The defect the audit office reported as AF-0571 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0571 (Low, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0571-received-messagelargerthan64kib-checks-the-connect.md`.

Location: `Curl.Conformance.Extended.UnitTests/Mqtt5/PublishSubscribe/ReceivedPublishCases.cs:145`

Location: `Curl.Conformance.Extended.UnitTests/Mqtt5/PublishSubscribe/ReceivedPublishCases.cs:145`

Step 0 weak-assertion. The name says the message is written whole because curl announces no Maximum Packet Size; the only check of the CONNECT curl sent is Assert.IsNotNull(broker.Connect);, so a CONNECT that did carry a Maximum Packet Size property would pass.

Reproduction, from the finding:

Run from the repository root:

```powershell
Select-String -Path Curl.Conformance.Extended.UnitTests/Mqtt5/PublishSubscribe/ReceivedPublishCases.cs -Pattern 'Assert.IsNotNull\(broker.Connect\)'
```

- Expected: No match: the test asserts the CONNECT's properties hold no Maximum Packet Size.
- Actual: ReceivedPublishCases.cs:145: Assert.IsNotNull(broker.Connect);

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-11-03: Created.
