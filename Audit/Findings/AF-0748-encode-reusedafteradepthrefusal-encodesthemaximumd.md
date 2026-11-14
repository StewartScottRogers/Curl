---
id: AF-0748
title: Encode_ReusedAfterADepthRefusal_EncodesTheMaximumDepthAgain checks only that some bytes came back, not the encoding
auditor: quality
severity: Low
status: accepted
reason: Accepted under Stewart's standing accept of 2026-11-02.
key: quality:Curl.Original.Protocol.Ldap.UnitTests/LdapAdversarialTests.cs:Encode_ReusedAfterADepthRefusal_EncodesTheMaximumDepthAgain:weak-assertion
reproduction: none
task: none
tasks:
found: 2026-11-14
found-at: fc4d1268bd4c1bce123dba90334b07e141be698a
scorecard: 2026-11-14_1335.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0748 - Encode_ReusedAfterADepthRefusal_EncodesTheMaximumDepthAgain checks only that some bytes came back, not the encoding

## Summary

Low finding from the quality auditor at `Curl.Original.Protocol.Ldap.UnitTests/LdapAdversarialTests.cs:397`: Encode_ReusedAfterADepthRefusal_EncodesTheMaximumDepthAgain checks only that some bytes came back, not the encoding.

## Evidence

Location: `Curl.Original.Protocol.Ldap.UnitTests/LdapAdversarialTests.cs:397`

The test refuses a too-deep filter, reuses the encoder for the maximum depth and asserts only Assert.IsNotNull(reused);. The point of the test is that state left by the refusal does not corrupt the next encoding, yet a reused encoder that emits wrong or truncated bytes would pass. Comparing with a fresh encoder's Encode(NestedAnd(MaximumSetDepth)) would catch it.

## Reproduction

Run from the repository root:

```powershell
Select-String -Path Curl.Original.Protocol.Ldap.UnitTests/LdapAdversarialTests.cs -Pattern 'Encode_ReusedAfterADepthRefusal_EncodesTheMaximumDepthAgain' -Context 0,8
```

- Expected: An exact comparison of the reused encoder's bytes with a fresh encoding.
- Actual: Assert.IsNotNull(reused); is the only assertion on the re-encoded bytes.

## Re-audits

## Log

- 2026-11-14: filed proposed.
