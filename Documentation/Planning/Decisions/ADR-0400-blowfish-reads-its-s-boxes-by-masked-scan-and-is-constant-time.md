# ADR-0400 — Blowfish reads its S-boxes by masked scan and is constant-time

- **Status:** Accepted
- **Date:** 2026-10-02

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-1273.
Amends ADR-0118's "Constant time and zeroing" section and ADR-0399's statement that
Blowfish is the only type that is not constant-time.

## Context

`BlowfishState`'s F function read each of the four S-boxes at an index taken from a byte
of the half-block. The S-boxes are key-expanded, so the address of every look-up depended
on the key and the data. In `BcryptPbkdf` the key is the SSH private-key passphrase, so
the cache-timing channel reached the passphrase as well as SSH `blowfish-cbc` session
keys. The audit office's security auditor reported it as AF-0017 (High), Stewart accepted
the finding, and BL-1273 fixes it, as ADR-0396, ADR-0398 and ADR-0399 did for DES,
CAST-128 and RC4.

## Decision

`BlowfishState.Mix`, the F function, makes one pass over all 1,024 S-box words, in order,
a `Vector<uint>` at a time. For each chunk it compares the chunk's positions with each
box's index (`Vector.Equals`) and ORs the masked entries into one accumulator per box;
at the end only one lane of each accumulator is non-zero, so `Vector.Sum` yields the
entry. The loads are `Vector.LoadUnsafe` at fixed offsets: the loop bound is the constant
256, a multiple of every vector width, so no load leaves the array, and the load and
compare are JIT intrinsics even in Debug builds, where span slicing kept the tests five
times slower. No memory address, branch or loop bound depends on the key, the passphrase
or the block, so `Blowfish` and `BcryptPbkdf` are constant-time and their XML
documentation says so. Every hand-built cipher in `Curl.Cryptography.UnitLibrary` now
reads its tables by masked scan.

## Consequences

- An F function costs 256 / width vector iterations (32 with AVX2) instead of four
  look-ups. One bcrypt hash (about 67,000 block encryptions) takes about 50 ms in a
  Release build; OpenSSH's default 16 rounds for a 48-byte key and IV take about 1.6 s.
  Blowfish is a legacy SSH cipher and `bcrypt_pbkdf` is deliberately slow; the cost is
  acceptable there.
- The output is unchanged; Schneier's and Go's known answers still pass, and a new test
  checks `Mix` against the direct look-up formula at every index of every box.

## Alternatives considered

- **Leave it table-indexed, documented as not constant-time:** what ADR-0118 accepted, and
  what the finding showed leaks the passphrase.
- **A scalar masked scan with `ConstantTime.EqualMask`, as RC4 does:** correct, but 1,024
  scalar iterations per F function made one bcrypt hash take seconds.
- **Read one word per cache line:** cheaper, but still leaks the offset within a line to
  cache-bank attacks (CacheBleed), so it is not constant-time.
