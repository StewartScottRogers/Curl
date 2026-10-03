# ADR-0398 — CAST-128 reads its S-boxes by masked scan and is constant-time

- **Status:** Accepted
- **Date:** 2026-10-02

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-1271.
Amends ADR-0118's "Constant time and zeroing" section and ADR-0396's list of types that
are not constant-time.

## Context

`Cast128.Round` indexed RFC 2144's S1 to S4 with the four bytes of the half-block combined
with the masking subkey and rotated by the rotation subkey, so the address of each look-up
depended on the key and the data. The key schedule indexed S5 to S8 with key bytes the
same way. SSH's `cast128-cbc` keys CAST-128 with session keys, so the cache-timing channel
leaked key- and plaintext-dependent bits. The audit office's security auditor reported it
as AF-0015 (High), Stewart accepted the finding, and BL-1271 fixes it, as ADR-0393,
ADR-0395 and ADR-0396 did for Camellia, ARIA and DES.

## Decision

Every S-box read in `Cast128` - the four in the round function and the five per row of the
key schedule - goes through `Cast128.ReadBox`, which reads all 256 entries of the box in
order, once, and keeps the entry whose position equals the index, chosen by
`ConstantTime.EqualMask`. The table index depends only on the loop position. No memory
address, branch or loop bound depends on the key or the data, so `Cast128` is
constant-time and its XML documentation says so. The types that are not constant-time are
now Blowfish and RC4.

## Consequences

- A round is four scans of 256 entries, 1,024 iterations; a 16-round block is about
  16,000, and the key schedule about 82,000 once per key. CAST-128 is a legacy SSH cipher
  offered last; the cost is acceptable for a cipher kept only for compatibility.
- The output is unchanged; RFC 2144 Appendix B's known-answer tests still pass.

## Alternatives considered

- **Leave it table-indexed, documented as not constant-time:** what ADR-0118 accepted, and
  what the finding showed leaks session-key material on the wire.
- **Bitsliced CAST-128:** CAST-128's 8-to-32-bit S-boxes have no compact Boolean circuit,
  so bitslicing them is far more code than the scan.
