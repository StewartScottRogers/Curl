---
id: AF-0970
title: BrainpoolEcdsa.Verify compares the recomputed r with the signature's r using SequenceEqual, unlike DsaSignature, Ed25519 and Ed448
auditor: security
severity: High
status: closed
reason: Re-audit 2026-11-22_1351.md: the reproduction no longer reproduces.
key: security:Curl.Cryptography.UnitLibrary/BrainpoolEcdsa.cs:Verify:non-constant-time-compare
reproduction: none
task: BL-5741
tasks: BL-5741
found: 2026-11-22
found-at: 9f9f37761162551e34c06d99ea4c7ebfb0168e83
scorecard: 2026-11-22_0210.md
duplicate-of:
closed: 2026-11-22
closed-how: reliable-reaudit
closed-by: 2026-11-22_1351.md
---
# AF-0970 - BrainpoolEcdsa.Verify compares the recomputed r with the signature's r using SequenceEqual, unlike DsaSignature, Ed25519 and Ed448

## Summary

High finding from the security auditor at `Curl.Cryptography.UnitLibrary/BrainpoolEcdsa.cs:186`: BrainpoolEcdsa.Verify compares the recomputed r with the signature's r using SequenceEqual, unlike DsaSignature, Ed25519 and Ed448.

## Evidence

Location: `Curl.Cryptography.UnitLibrary/BrainpoolEcdsa.cs:186`

Line 186 is `return finite && w.SequenceEqual(r);`. w is x_R mod q recomputed from the public key, the hash and the signature, and r is the signature value. SequenceEqual returns at the first differing limb, so the time taken depends on how many leading limbs of a forged signature match. The sibling verifiers use FixedTimeEquals: DsaSignature.cs:238, Ed25519.cs:130 and Ed448.cs:173. Method step 2 requires a signature comparison to run in constant time. Mitigating: both operands are derived from public inputs, so the exploitable impact is low, but this is the library's own convention and the only departure from it.

## Reproduction

Run from the repository root:

```powershell
Select-String -Path Curl.Cryptography.UnitLibrary/BrainpoolEcdsa.cs -Pattern "SequenceEqual|FixedTimeEquals"
```

- Expected: return finite && CryptographicOperations.FixedTimeEquals(MemoryMarshal.AsBytes(w), MemoryMarshal.AsBytes(r));
- Actual: BrainpoolEcdsa.cs:186:        return finite && w.SequenceEqual(r);

## Re-audits

- 2026-11-22 | 2026-11-22_1351.md | reproduces: no | Select-String on Curl.Cryptography.UnitLibrary/BrainpoolEcdsa.cs for SequenceEqual|FixedTimeEquals finds only line 186: return finite && CryptographicOperations.FixedTimeEquals(MemoryMarshal.AsBytes(w), MemoryMarshal.AsBytes(r)). No SequenceEqual.

## Log

- 2026-11-22: filed proposed.
- 2026-11-22: accepted -> closed. Re-audit 2026-11-22_1351.md: the reproduction no longer reproduces.
