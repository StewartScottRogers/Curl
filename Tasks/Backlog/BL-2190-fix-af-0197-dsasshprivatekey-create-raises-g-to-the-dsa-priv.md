---
id: BL-2190
title: Fix AF-0197: DsaSshPrivateKey.Create raises g to the DSA private key x through variable-time BigInteger.ModPow
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Ssh.UnitLibrary]
requirement: none
created: 2026-10-14
completed:
---
# BL-2190 — Fix AF-0197: DsaSshPrivateKey.Create raises g to the DSA private key x through variable-time BigInteger.ModPow

## Goal

The defect the audit office reported as AF-0197 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0197 (High, security auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0197-dsasshprivatekey-create-raises-g-to-the-dsa-privat.md`.

Location: `Curl.Protocol.Ssh.UnitLibrary/Keys/DsaSshPrivateKey.cs:61`

Location: `Curl.Protocol.Ssh.UnitLibrary/Keys/DsaSshPrivateKey.cs:61`

Line 61: 'byte[] y = publicKey ?? BigInteger.ModPow(Unsigned(generator), Unsigned(privateKey), Unsigned(prime)).ToByteArray(isUnsigned: true, isBigEndian: true);'. privateKey is the DSA secret x, read from the user's PKCS #8 key file (which leaves y out, so this branch runs for every such key). BigInteger.ModPow is square-and-multiply that branches on the exponent's bits and its limb count, so its running time depends on x. Curl.Cryptography's own rule is never BigInteger on a secret, and it already offers the constant-time fixed-window DsaDomainParameters.RaiseGenerator (MontgomeryModulus.Exponentiate) for exactly g^x mod p. This is the same class of defect as AF-0182. Exposure is local: it runs once per key load. Phase 2: ADR-0230 covers DSA key loading and signing but says nothing about this exponentiation.

Reproduction, from the finding:

Run from the repository root:

```powershell
Select-String -Path Curl.Protocol.Ssh.UnitLibrary/Keys/DsaSshPrivateKey.cs -Pattern 'BigInteger\.ModPow'
```

- Expected: No match: y is computed with the constant-time exponentiation in Curl.Cryptography.
- Actual: DsaSshPrivateKey.cs:61:        byte[] y = publicKey ?? BigInteger.ModPow(Unsigned(generator), Unsigned(privateKey), Unsigned(prime)).ToByteArray(isUnsigned: true, isBigEndian: true);

The finding closes only when a later re-audit by the security auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-14: Created.
