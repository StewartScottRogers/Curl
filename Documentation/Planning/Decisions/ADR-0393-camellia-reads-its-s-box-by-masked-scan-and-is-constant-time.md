# ADR-0393 — Camellia reads its S-box by masked scan and is constant-time

- **Status:** Accepted
- **Date:** 2026-10-02

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-1268.
Supersedes ADR-0145 and amends ADR-0118's "Constant time and zeroing" section.

## Context

ADR-0145 let `Camellia`'s F-function index RFC 3713's fixed S-boxes with key-mixed data
bytes, as OpenSSL's and LibreSSL's Camellia do, so the address of each look-up depended on
the key and the data (a cache-timing channel). The audit office's security auditor reported
it as AF-0012 (High), Stewart accepted the finding, and BL-1268 fixes it.

## Decision

The F-function gathers its eight S-box indices into one 64-bit word (applying SBOX4's
input rotation first) and passes it to `Camellia.SubstituteBytes`, which reads all 256
entries of `SBOX1` in order, once, and keeps each entry in every byte of the word whose
index equals its position, chosen by a branch-free per-byte zero test. SBOX2's and
SBOX3's output rotations are applied afterwards. No memory address, branch or loop bound
depends on the key or the data, so `Camellia` is constant-time and its XML documentation
says so. ADR-0118's list of types that are not constant-time becomes Blowfish, CAST-128,
RC4 and ARIA.

## Consequences

- One table scan serves all eight look-ups of a round: 256 iterations of a handful of
  64-bit operations, roughly 18 or 24 scans a block. A Camellia-CBC transfer is slower
  than OpenSSL's table-indexed one, which is acceptable: curl's defaults prefer AES-GCM
  and ChaCha20-Poly1305, so a Camellia suite runs only when a server picks one.
- The output is unchanged; RFC 3713's test vectors and the CBC tests still pass.
- A bitsliced S-box (GF(2^8) inversion and affine maps) stays possible later if the
  scan's speed matters.
