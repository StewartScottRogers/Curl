---
id: BL-2141
title: Fix AF-0182: SrpClient exponentiates with the password-derived x and private a through variable-time BigInteger.ModPow
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Tls.UnitLibrary, Curl.Tls.UnitTests]
requirement: none
created: 2026-10-13
completed: 2026-10-13
---
# BL-2141 — Fix AF-0182: SrpClient exponentiates with the password-derived x and private a through variable-time BigInteger.ModPow

## Goal

The defect the audit office reported as AF-0182 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0182 (High, security auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0182-srpclient-exponentiates-with-the-password-derived.md`.

Location: `Curl.Tls.UnitLibrary/SrpClient.cs:86`

Location: `Curl.Tls.UnitLibrary/SrpClient.cs:86`

ComputePremasterSecret (lines 76-87) runs 'BigInteger.ModPow(group.GeneratorValue, x, n)' with x = SHA1(s | SHA1(I | ':' | P)) from the user's TLS-SRP password (line 84), and 'BigInteger.ModPow(basis, ToInteger(privateValue) + (u * x), n)' with the secret exponent a + u*x (line 86). ComputePublicValue (line 53) and ComputeVerifier (line 43) also run ModPow on a and x. System.Numerics.BigInteger.ModPow is a general square-and-multiply on variable-length magnitudes: its branches, its multiply count and its operand lengths depend on the exponent's bits and on the size of intermediate values, and it makes no constant-time guarantee. Tls12ClientHandshake.cs:663-667 calls it on every SRP handshake with credentials.Password. A timing observer of the client's ClientKeyExchange can learn about x (an offline-attackable password hash) and a. OpenSSL's SRP marks these exponents BN_FLG_CONSTTIME. Phase 2: explained by ADR-0229 ('SrpClient holds the arithmetic ... over System.Numerics.BigInteger'), which picks BigInteger but does not mention timing. The library already has a constant-time MontgomeryModulus exponentiation in Curl.Cryptography.UnitLibrary.

Reproduction, from the finding:

Run from the repository root:

```powershell
Select-String -Path Curl.Tls.UnitLibrary/SrpClient.cs -Pattern 'BigInteger\.ModPow'
```

- Expected: No match: the secret exponents x, a and a + u*x go through a fixed-window constant-time exponentiation (for example Curl.Cryptography's MontgomeryModulus).
- Actual: SrpClient.cs:43: return ToBytes(BigInteger.ModPow(group.GeneratorValue, ToInteger(privateKey), group.PrimeValue)); SrpClient.cs:53: return ToBytes(BigInteger.ModPow(group.GeneratorValue, ToInteger(privateValue), group.PrimeValue)); SrpClient.cs:84: BigInteger kv = k * BigInteger.ModPow(group.GeneratorValue, x, n) % n; SrpClient.cs:86: return ToBytes(BigInteger.ModPow(basis, ToInteger(privateValue) + (u * x), n));

The finding closes only when a later re-audit by the security auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Fix: `SrpClient` now raises every secret exponent (x, a, a + u * x) through `Curl.Cryptography`'s public `FiniteFieldDiffieHellman` (fixed-window Montgomery), on a group cached as `SrpGroup.ExponentiationGroup`; ADR-0481 records why (amends ADR-0229). The bases 0, 1 and N - 1, which that API refuses, are answered without exponentiation and pinned by `SrpClientTests.PremasterSecretOfADegenerateBasisIsItsPower`.
- Added `Curl.Tls.UnitTests` to `touches` for that test: no task in Doing on `origin/work/dark-factory` named it.
- Reproduction: `Select-String ... BigInteger.ModPow` on `SrpClient.cs` finds no match. `Measure-CodeQuality.ps1 -Library Curl.Tls.UnitLibrary`: 0 failing members. Full build clean, fast tests green.

## Log

- 2026-10-13: Created.
- 2026-10-13: Backlog -> Doing.
- 2026-10-13: Doing -> Done. SrpClient's secret exponents run through the constant-time FiniteFieldDiffieHellman; reproduction finds no BigInteger.ModPow
