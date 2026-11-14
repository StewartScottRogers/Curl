---
id: AF-0690
title: Config_KeyId_IsNhOctetsAndFollowsTheKey never changes the key: it changes the AEAD
auditor: quality
severity: Medium
status: closed
reason: Re-audit 2026-11-13_2321.md: the reproduction no longer reproduces.
key: quality:Curl.Extension.ObliviousDoh.UnitTests/Wire/ObliviousDohWireTests.cs:Config_KeyId_IsNhOctetsAndFollowsTheKey:name-lies
reproduction: none
task: BL-4884
tasks: BL-4884
found: 2026-11-05
found-at: 4eba3c63638befcc6a63aaa31ddf5419922fa939
scorecard: 2026-11-05_2025.md
duplicate-of:
closed: 2026-11-13
closed-how: reliable-reaudit
closed-by: 2026-11-13_2321.md
---
# AF-0690 - Config_KeyId_IsNhOctetsAndFollowsTheKey never changes the key: it changes the AEAD

## Summary

Medium finding from the quality auditor at `Curl.Extension.ObliviousDoh.UnitTests/Wire/ObliviousDohWireTests.cs:57`: Config_KeyId_IsNhOctetsAndFollowsTheKey never changes the key: it changes the AEAD.

## Evidence

Location: `Curl.Extension.ObliviousDoh.UnitTests/Wire/ObliviousDohWireTests.cs:57`

The test asserts Assert.HasCount(32, keyId) and CollectionAssert.AreNotEqual(keyId, new ObliviousDohConfig(HpkeKem.DhkemP256HkdfSha256, HpkeKdf.HkdfSha256, HpkeAead.Aes256Gcm, P256Key).KeyId()). Both configs use the same P256Key and differ only in AEAD. A key id that ignored the public key would pass, so 'follows the key' is never tested. It also checks no exact RFC 9230 section 5 value.

## Reproduction

Run from the repository root:

```powershell
Select-String -Path Curl.Extension.ObliviousDoh.UnitTests/Wire/ObliviousDohWireTests.cs -Pattern 'FollowsTheKey' -Context 0,6
```

- Expected: The comparison config uses a different public key, or the key id is checked against an exact vector.
- Actual: The comparison uses the same P256Key with HpkeAead.Aes256Gcm in place of Aes128Gcm.

## Re-audits

- 2026-11-12 | 2026-11-12_2105.md | reproduces: no | Ran the Select-String: 'FollowsTheKey' no longer matches. The test is now Config_KeyId_FollowsThePublicKey (ObliviousDohWireTests.cs:78), which compares KeyId() for P256Key against otherKey with the same KEM, KDF and AEAD.
- 2026-11-13 | 2026-11-13_2321.md | reproduces: no | No test named FollowsTheKey remains; Config_KeyId_FollowsThePublicKey (line 78) builds both configs with the same KEM, KDF and AEAD and a different public key (otherKey) and asserts CollectionAssert.AreNotEqual on the key ids.

## Log

- 2026-11-05: filed proposed.
- 2026-11-13: accepted -> closed. Re-audit 2026-11-13_2321.md: the reproduction no longer reproduces.
