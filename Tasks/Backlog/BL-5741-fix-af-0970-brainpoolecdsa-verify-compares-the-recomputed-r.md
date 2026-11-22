---
id: BL-5741
title: Fix AF-0970: BrainpoolEcdsa.Verify compares the recomputed r with the signature's r using SequenceEqual, unlike DsaSignature, Ed25519 and Ed448
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Cryptography.UnitLibrary/BrainpoolEcdsa.cs]
requirement: none
created: 2026-11-22
completed:
---
# BL-5741 — Fix AF-0970: BrainpoolEcdsa.Verify compares the recomputed r with the signature's r using SequenceEqual, unlike DsaSignature, Ed25519 and Ed448

## Goal

The defect the audit office reported as AF-0970 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0970 (High, security auditor), which Stewart accepted. The finding's file is on the audit office's paths, which a dark factory lane may not read or run; everything this task needs is below.

Location: `Curl.Cryptography.UnitLibrary/BrainpoolEcdsa.cs:186`

Location: `Curl.Cryptography.UnitLibrary/BrainpoolEcdsa.cs:186`

Line 186 is `return finite && w.SequenceEqual(r);`. w is x_R mod q recomputed from the public key, the hash and the signature, and r is the signature value. SequenceEqual returns at the first differing limb, so the time taken depends on how many leading limbs of a forged signature match. The sibling verifiers use FixedTimeEquals: DsaSignature.cs:238, Ed25519.cs:130 and Ed448.cs:173. Method step 2 requires a signature comparison to run in constant time. Mitigating: both operands are derived from public inputs, so the exploitable impact is low, but this is the library's own convention and the only departure from it.

Reproduction, from the finding:

Run from the repository root:

```powershell
Select-String -Path Curl.Cryptography.UnitLibrary/BrainpoolEcdsa.cs -Pattern "SequenceEqual|FixedTimeEquals"
```

- Expected: return finite && CryptographicOperations.FixedTimeEquals(MemoryMarshal.AsBytes(w), MemoryMarshal.AsBytes(r));
- Actual: BrainpoolEcdsa.cs:186:        return finite && w.SequenceEqual(r);

The finding closes only when a later re-audit by the security auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-11-22: Created.
