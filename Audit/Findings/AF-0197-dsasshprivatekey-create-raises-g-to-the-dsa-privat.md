---
id: AF-0197
title: DsaSshPrivateKey.Create raises g to the DSA private key x through variable-time BigInteger.ModPow
auditor: security
severity: High
status: accepted
reason: 
key: security:Curl.Protocol.Ssh.UnitLibrary/Keys/DsaSshPrivateKey.cs:DsaSshPrivateKey.Create:secret-dependent-branch
reproduction: none
task: none
tasks:
found: 2026-10-13
found-at: c192c3e80e58636be1d282d241a7781f7df86695
scorecard: 2026-10-13_1822.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0197 - DsaSshPrivateKey.Create raises g to the DSA private key x through variable-time BigInteger.ModPow

## Summary

High finding from the security auditor at `Curl.Protocol.Ssh.UnitLibrary/Keys/DsaSshPrivateKey.cs:61`: DsaSshPrivateKey.Create raises g to the DSA private key x through variable-time BigInteger.ModPow.

## Evidence

Location: `Curl.Protocol.Ssh.UnitLibrary/Keys/DsaSshPrivateKey.cs:61`

Line 61: 'byte[] y = publicKey ?? BigInteger.ModPow(Unsigned(generator), Unsigned(privateKey), Unsigned(prime)).ToByteArray(isUnsigned: true, isBigEndian: true);'. privateKey is the DSA secret x, read from the user's PKCS #8 key file (which leaves y out, so this branch runs for every such key). BigInteger.ModPow is square-and-multiply that branches on the exponent's bits and its limb count, so its running time depends on x. Curl.Cryptography's own rule is never BigInteger on a secret, and it already offers the constant-time fixed-window DsaDomainParameters.RaiseGenerator (MontgomeryModulus.Exponentiate) for exactly g^x mod p. This is the same class of defect as AF-0182. Exposure is local: it runs once per key load. Phase 2: ADR-0230 covers DSA key loading and signing but says nothing about this exponentiation.

## Reproduction

Run from the repository root:

```powershell
Select-String -Path Curl.Protocol.Ssh.UnitLibrary/Keys/DsaSshPrivateKey.cs -Pattern 'BigInteger\.ModPow'
```

- Expected: No match: y is computed with the constant-time exponentiation in Curl.Cryptography.
- Actual: DsaSshPrivateKey.cs:61:        byte[] y = publicKey ?? BigInteger.ModPow(Unsigned(generator), Unsigned(privateKey), Unsigned(prime)).ToByteArray(isUnsigned: true, isBigEndian: true);

## Re-audits

## Log

- 2026-10-13: filed proposed.
- 2026-10-13: proposed -> accepted.
