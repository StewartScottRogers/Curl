---
id: AF-0571
title: Received_MessageLargerThan64KiB checks the CONNECT only for null, not that it announces no Maximum Packet Size
auditor: quality
severity: Low
status: accepted
reason: Accepted under Stewart's standing accept of 2026-11-02.
key: quality:Curl.Conformance.Extended.UnitTests/Mqtt5/PublishSubscribe/ReceivedPublishCases.cs:Received_MessageLargerThan64KiB_IsWrittenWholeSinceCurlAnnouncesNoMaximumPacketSize:weak-assertion
reproduction: none
task: BL-4735
tasks: BL-4735
found: 2026-11-03
found-at: f4a738a932020f9832b23c0977dbaf0f27206871
scorecard: 2026-11-03_1017.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0571 - Received_MessageLargerThan64KiB checks the CONNECT only for null, not that it announces no Maximum Packet Size

## Summary

Low finding from the quality auditor at `Curl.Conformance.Extended.UnitTests/Mqtt5/PublishSubscribe/ReceivedPublishCases.cs:145`: Received_MessageLargerThan64KiB checks the CONNECT only for null, not that it announces no Maximum Packet Size. Reported by an auditor flagged unreliable in 2026-11-03_1017.md.

## Evidence

Location: `Curl.Conformance.Extended.UnitTests/Mqtt5/PublishSubscribe/ReceivedPublishCases.cs:145`

Step 0 weak-assertion. The name says the message is written whole because curl announces no Maximum Packet Size; the only check of the CONNECT curl sent is Assert.IsNotNull(broker.Connect);, so a CONNECT that did carry a Maximum Packet Size property would pass.

## Reproduction

Run from the repository root:

```powershell
Select-String -Path Curl.Conformance.Extended.UnitTests/Mqtt5/PublishSubscribe/ReceivedPublishCases.cs -Pattern 'Assert.IsNotNull\(broker.Connect\)'
```

- Expected: No match: the test asserts the CONNECT's properties hold no Maximum Packet Size.
- Actual: ReceivedPublishCases.cs:145: Assert.IsNotNull(broker.Connect);

## Re-audits

- 2026-11-03 | 2026-11-03_1615.md | reproduces: no | No 'Assert.IsNotNull(broker.Connect)' match; line 146 asserts CollectionAssert.DoesNotContain(broker.Connect!.PropertyIdentifiers.ToList(), maximumPacketSizeProperty).

## Log

- 2026-11-03: filed proposed.
- 2026-11-03: proposed -> accepted. Accepted under Stewart's standing accept of 2026-11-02.
