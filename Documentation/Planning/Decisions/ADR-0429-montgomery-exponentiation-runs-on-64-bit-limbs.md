# ADR-0429 — `MontgomeryModulus.Exponentiate` runs on 64-bit limbs, with carries taken from unsigned comparisons

- Status: Accepted
- Date: 2026-10-07
- Task: BL-1656
- Decided by Claude under Stewart's delegation.

## Context

A TLS 1.3 handshake on `ffdhe8192` spent seconds in `FiniteFieldDiffieHellman`: every
exponentiation ran 655 Montgomery multiplications on 256 32-bit limbs, through small helper
methods (`AddProduct`, `ReduceOneLimb`) that the JIT kept at tier 0 for a long time in a
busy process, because each call's loop is too short for on-stack replacement. Native AOT
curl compiles everything optimized, but the 32-bit limbs still cost four times the
inner-loop steps of 64-bit ones. RSA, DSA and the brainpool curves call the same class
with spans of 32-bit limbs and scratch sized for them.

## Decision

- The public surface of `MontgomeryModulus` keeps 32-bit limbs. `Exponentiate` alone
  converts to 64-bit limbs at its edges and works in its own Montgomery radix
  R' = 2^(64 * ceil(n / 2)), whose constants R' mod m and R'^2 mod m the constructor
  derives by doubling R mod m and R^2 mod m on (32 more doublings each when the 32-bit
  limb count n is odd, none when it is even). Base and result are in ordinary form, so
  callers see no difference.
- The 64-bit products come from `Math.BigMul(ulong, ulong, out ulong)` (`mulx` on x64,
  `umulh` on Arm64, both constant-time). Squarings, four of every five operations, have
  their own routine that computes each cross product once.
- A carry or borrow is `Unsafe.BitCast<bool, byte>(sum < left)`: a comparison taken as a
  value compiles to IL `clt.un`, which the JIT turns into `setb` (x64) or `cset` (Arm64),
  never a branch - checked in the JIT's disassembly of `MultiplyWide`. It is half the
  instructions of the bitwise carry formula and measured faster.
- The hot 64-bit routines carry `MethodImplOptions.AggressiveOptimization`, so a JIT
  process runs them optimized from the first call instead of at tier 0.
- `Exponentiate` has an internal overload that records its steps (`square`, `select`,
  `multiply`), so a test pins that the sequence depends only on the exponent's length.

## Why

The exponentiation is where all the time goes, and changing it alone leaves every other
caller's code, scratch sizes and tests untouched. Measured on one lane machine, first call
in a fresh process (as one curl run makes it), four ffdhe8192 exponentiations went from
444 to 828 ms to 107 to 117 ms; warm, from 177 to 318 ms to 82 to 96 ms.

## Consequences

`Multiply`, `Add` and the rest still run on 32-bit limbs; moving them to 64-bit limbs would
speed up the brainpool curves too, and is a separate change. The constructor does up to 96
more modular doublings for an odd limb count.
