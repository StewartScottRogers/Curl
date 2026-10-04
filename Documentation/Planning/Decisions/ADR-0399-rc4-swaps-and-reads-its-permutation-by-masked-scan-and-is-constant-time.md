# ADR-0399 — RC4 swaps and reads its permutation by masked scan and is constant-time

- **Status:** Accepted
- **Date:** 2026-10-02

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-1272.
Amends ADR-0118's "Constant time and zeroing" section and ADR-0398's list of types that
are not constant-time.

## Context

`Rc4` indexed its key-dependent permutation with key-dependent positions: the key
schedule swapped entry `i` with entry `j`, where `j` mixes in the key, and each keystream
byte swapped entry `i` with the secret `j` and read the entry at `S[i] + S[j]`. The
address of each of those accesses depended on the key, a cache-timing channel on SSH
`arcfour` session keys and NTLM / Kerberos `rc4-hmac` keys. The audit office's security
auditor reported it as AF-0016 (High), Stewart accepted the finding, and BL-1272 fixes it,
as ADR-0396 and ADR-0398 did for DES and CAST-128.

## Decision

Only the counter `i` is public. Every access at a secret position goes through one of two
internal helpers, each a single pass over all 256 entries in order:

- `Rc4.SwapWithSecretIndex` reads `S[i]` at the public index, then for every position
  keeps the entry at the secret index and overwrites it with `S[i]` under a mask from
  `ConstantTime.EqualMask`, rewriting every other entry with itself; finally it writes the
  kept entry to `S[i]`. It returns the old `S[j]`, which is the new `S[i]`.
- `Rc4.ReadAtSecretIndex` reads every entry and keeps the one at the secret index by mask.

The key schedule does one swap per position; a keystream byte does one swap and one read,
and computes the output index from the two values it already holds, never by re-reading
the permutation. No memory address, branch or loop bound depends on the key, so `Rc4` is
constant-time and its XML documentation says so. Blowfish is now the only type that is
not.

## Consequences

- A keystream byte costs two passes of 256 entries (512 iterations) instead of four
  look-ups, and the key schedule about 65,000 iterations once per key. RC4 is a legacy
  cipher kept for SSH `arcfour` and NTLM / Kerberos `rc4-hmac` compatibility; the cost is
  acceptable there.
- The output is unchanged; RFC 6229's and RFC 4345's known answers still pass.

## Alternatives considered

- **Leave it table-indexed, documented as not constant-time:** what ADR-0118 accepted, and
  what the finding showed leaks key material.
- **Separate read and write passes per swap:** correct, but one combined pass does the
  same work in half the iterations.
