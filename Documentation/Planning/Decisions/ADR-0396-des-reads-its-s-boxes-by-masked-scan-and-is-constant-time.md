# ADR-0396 — DES reads its S-boxes by masked scan and is constant-time

- **Status:** Accepted
- **Date:** 2026-10-02

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-1270.
Supersedes ADR-0156's acceptance of a table-indexed DES (its choice to hand-build DES and
accept every weak key stands) and amends ADR-0118's "Constant time and zeroing" section.

## Context

`Des.Round` indexed FIPS 46-3's eight S-boxes with the expanded half-block XOR the round
key, so the address of each look-up depended on the key. NTLM keys DES with
password-derived material (`LMOWFv1` and `DESL`), so the cache-timing channel leaked
password-derived bits. The audit office's security auditor reported it as AF-0014
(High), Stewart accepted the finding, and BL-1270 fixes it, as ADR-0393 and ADR-0395 did
for Camellia and ARIA.

## Decision

`Des.Round` passes each six-bit S-box input to `Des.SubstituteSix`, which reads all 64
entries of that box in order, once, and keeps the entry whose six selecting bits (row from
the outer two, column from the inner four) equal the input, chosen by the branch-free mask
`((six ^ position) - 1) >> 31`. The table index depends only on the loop position. No
memory address, branch or loop bound depends on the key or the data, so `Des` is
constant-time and its XML documentation says so. ADR-0118's list of types that are not
constant-time becomes Blowfish, CAST-128 and RC4.

## Consequences

- A round is eight scans of 64 entries, 512 iterations; a block is about 8,000. NTLM
  runs DES on a handful of blocks per authentication, so the cost does not show.
- The output is unchanged; the FIPS and NTLM known-answer tests still pass.
- Permutations shift by public table positions only, so they need no change.

## Alternatives considered

- **Bitsliced DES:** constant-time and fast for many blocks, but far more code for the
  few blocks NTLM encrypts.
- **The BCL's `DES`:** refuses weak keys, which an empty password's LM hash needs
  (ADR-0156), and its timing depends on the platform's provider.
