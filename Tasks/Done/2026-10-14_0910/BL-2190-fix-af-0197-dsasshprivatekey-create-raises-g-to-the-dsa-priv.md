---
id: BL-2190
title: Fix AF-0197: DsaSshPrivateKey.Create raises g to the DSA private key x through variable-time BigInteger.ModPow
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Ssh.UnitLibrary, Curl.Cryptography.UnitLibrary, Curl.Cryptography.UnitTests]
requirement: none
created: 2026-10-14
completed: 2026-10-13
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

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Fix: `DsaSignature` gains `ExportPublicKey` and `PublicKeyLength`, which compute y = g^x mod p with `DsaDomainParameters.RaiseGenerator` (the fixed-window `MontgomeryModulus.Exponentiate` signing already uses); `DsaSshPrivateKey.Create` calls it through the `DsaSignature` it already builds to check x, so no `BigInteger` touches x. Chosen over `FiniteFieldDiffieHellman` because that group rejects g = p - 1 and is named for key exchange, not DSA; mirrors `BrainpoolEcdsa.ExportPublicKey`.
- Touches widened to `Curl.Cryptography.UnitLibrary` and `Curl.Cryptography.UnitTests`: `DsaDomainParameters` is internal there, so the constant-time exponentiation needed a public member. No task in Doing on `origin/work/dark-factory` named either (BL-2192 Documentation, BL-2193 Dict).
- Tests: RFC 6979 A.2.1 and A.2.2 public keys, wrong destination length, after Dispose. Measure-CodeQuality on both libraries: 0 failing members.

## Log

- 2026-10-14: Created.
- 2026-10-13: Backlog -> Doing.
- 2026-10-13: Doing -> Done. DsaSshPrivateKey computes y with DsaSignature.ExportPublicKey's constant-time exponentiation; BigInteger.ModPow is gone
