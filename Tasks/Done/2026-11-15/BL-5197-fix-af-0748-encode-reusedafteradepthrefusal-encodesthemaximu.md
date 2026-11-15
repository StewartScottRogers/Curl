---
id: BL-5197
title: Fix AF-0748: Encode_ReusedAfterADepthRefusal_EncodesTheMaximumDepthAgain checks only that some bytes came back, not the encoding
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Original.Protocol.Ldap.UnitTests]
requirement: none
created: 2026-11-14
completed: 2026-11-14
---
# BL-5197 — Fix AF-0748: Encode_ReusedAfterADepthRefusal_EncodesTheMaximumDepthAgain checks only that some bytes came back, not the encoding

## Goal

The defect the audit office reported as AF-0748 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0748 (Low, quality auditor), which Stewart accepted. The finding's file is on the audit office's paths, which a dark factory lane may not read or run; everything this task needs is below.

Location: `Curl.Original.Protocol.Ldap.UnitTests/LdapAdversarialTests.cs:397`

Location: `Curl.Original.Protocol.Ldap.UnitTests/LdapAdversarialTests.cs:397`

The test refuses a too-deep filter, reuses the encoder for the maximum depth and asserts only Assert.IsNotNull(reused);. The point of the test is that state left by the refusal does not corrupt the next encoding, yet a reused encoder that emits wrong or truncated bytes would pass. Comparing with a fresh encoder's Encode(NestedAnd(MaximumSetDepth)) would catch it.

Reproduction, from the finding:

Run from the repository root:

```powershell
Select-String -Path Curl.Original.Protocol.Ldap.UnitTests/LdapAdversarialTests.cs -Pattern 'Encode_ReusedAfterADepthRefusal_EncodesTheMaximumDepthAgain' -Context 0,8
```

- Expected: An exact comparison of the reused encoder's bytes with a fresh encoding.
- Actual: Assert.IsNotNull(reused); is the only assertion on the re-encoded bytes.

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

Test now compares the reused encoder bytes with a fresh encoder via CollectionAssert.AreEqual. Ldap unit tests: 725 passed. Only a test changed; no production code.

## Log

- 2026-11-14: Created.
- 2026-11-14: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
- 2026-11-14: Doing -> Done. Reused-encoder test compares exact bytes with a fresh encoding
