---
id: AF-0690
title: Config_KeyId_IsNhOctetsAndFollowsTheKey never changes the key: it changes the AEAD
auditor: quality
severity: Medium
status: accepted
reason: Accepted under Stewart's standing accept of 2026-11-02.
key: quality:Curl.Extension.ObliviousDoh.UnitTests/Wire/ObliviousDohWireTests.cs:Config_KeyId_IsNhOctetsAndFollowsTheKey:name-lies
reproduction: none
task: none
tasks:
found: 2026-11-05
found-at: 4eba3c63638befcc6a63aaa31ddf5419922fa939
scorecard: 2026-11-05_2025.md
duplicate-of:
closed:
closed-how:
closed-by:
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

## Log

- 2026-11-05: filed proposed.
