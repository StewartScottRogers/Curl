# ADR-0395 — ARIA reads its S-boxes by masked scan and is constant-time

- **Status:** Accepted
- **Date:** 2026-10-02

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-1269.
Supersedes ADR-0147's choice to index ARIA's S-boxes directly (its GCM and GHASH choices
stand) and amends ADR-0118's "Constant time and zeroing" section.

## Context

ADR-0147 let `Aria`'s substitution layers SL1 and SL2 index RFC 5794's four fixed S-boxes
with key-mixed state bytes, as OpenSSL's ARIA does, so the address of each look-up
depended on the key and the data (a cache-timing channel in TLS's ARIA-GCM suites). The
audit office's security auditor reported it as AF-0013 (High), Stewart accepted the
finding, and BL-1269 fixes it, as ADR-0393 did for Camellia (AF-0012).

## Decision

`Aria.Substitute` splits the 128-bit state into two 64-bit halves and, for each of the four
S-boxes, passes both halves to `Aria.SubstituteBytes`, which reads all 256 entries of that
box in order, once, and keeps each entry in every byte whose index equals its position,
chosen by a branch-free per-byte zero test (the same scan as `Camellia.SubstituteBytes`).
A fixed lane mask then keeps only the bytes that RFC 5794 sends through that box: byte
`i` uses box `(i + first) % 4`, `first` being 0 for SL1 and 2 for SL2, which depends only
on the round number. No memory address, branch or loop bound depends on the key or the
data, so `Aria` is constant-time and its XML documentation says so. ADR-0118's list of
types that are not constant-time becomes Blowfish, CAST-128, RC4 and DES.

## Consequences

- A substitution layer is eight scans of 256 entries (four boxes, two halves): about
  2,000 iterations of a handful of 64-bit operations, 13 to 17 layers a block. ARIA-GCM is
  much slower than OpenSSL's table-indexed ARIA, which is acceptable: curl's defaults
  prefer AES-GCM and ChaCha20-Poly1305, so an ARIA suite runs only when a server picks one.
- The output is unchanged; RFC 5794's test vectors, the section 2.4.2 S-box examples and
  the SL1/SL2 inverse test still pass.
- A bitsliced S-box (GF(2^8) inversion and affine maps) stays possible later if speed
  matters.
