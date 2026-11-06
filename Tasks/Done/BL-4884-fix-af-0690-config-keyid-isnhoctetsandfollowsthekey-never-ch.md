---
id: BL-4884
title: Fix AF-0690: Config_KeyId_IsNhOctetsAndFollowsTheKey never changes the key: it changes the AEAD
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extension.ObliviousDoh.UnitTests]
requirement: none
created: 2026-11-06
completed: 2026-11-06
---
# BL-4884 — Fix AF-0690: Config_KeyId_IsNhOctetsAndFollowsTheKey never changes the key: it changes the AEAD

## Goal

The defect the audit office reported as AF-0690 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0690 (Medium, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0690-config-keyid-isnhoctetsandfollowsthekey-never-chan.md`.

Location: `Curl.Extension.ObliviousDoh.UnitTests/Wire/ObliviousDohWireTests.cs:57`

Location: `Curl.Extension.ObliviousDoh.UnitTests/Wire/ObliviousDohWireTests.cs:57`

The test asserts Assert.HasCount(32, keyId) and CollectionAssert.AreNotEqual(keyId, new ObliviousDohConfig(HpkeKem.DhkemP256HkdfSha256, HpkeKdf.HkdfSha256, HpkeAead.Aes256Gcm, P256Key).KeyId()). Both configs use the same P256Key and differ only in AEAD. A key id that ignored the public key would pass, so 'follows the key' is never tested. It also checks no exact RFC 9230 section 5 value.

Reproduction, from the finding:

Run from the repository root:

```powershell
Select-String -Path Curl.Extension.ObliviousDoh.UnitTests/Wire/ObliviousDohWireTests.cs -Pattern 'FollowsTheKey' -Context 0,6
```

- Expected: The comparison config uses a different public key, or the key id is checked against an exact vector.
- Actual: The comparison uses the same P256Key with HpkeAead.Aes256Gcm in place of Aes128Gcm.

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- The key id is now pinned to an exact RFC 9230 section 6.1 value (`Config_KeyId_IsExpandOfExtractOverTheContentsAsRfc9230Section6_1`), computed outside Curl with .NET's HKDF in PowerShell over the hand-laid config contents (0010 0001 0001 0041 04 and 64 zero octets); RFC 9230 publishes no test vector, so that independent computation is the reference. A second test (`Config_KeyId_FollowsThePublicKey`) compares two configs that share a suite and differ only in the public key.
- Test-only change: no production code changed, so no quality run was needed. `dotnet build` clean, fast tests green (Windows); the tests are platform-neutral.

## Log

- 2026-11-06: Created.
- 2026-11-06: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
- 2026-11-06: Doing -> Done. Key id pinned to an exact RFC 9230 value and checked to follow the public key
