# ADR-0147 — ARIA indexes its fixed S-boxes and is not constant-time, while its GHASH is

- **Status:** Accepted
- **Date:** 2026-09-28

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-784.
Amends ADR-0118's "Constant time and zeroing" section, as ADR-0145 did for Camellia.

## Context

ADR-0140 adds ARIA (RFC 5794) and ARIA-GCM to ADR-0118's list for the TLS
`*-ARIA*-GCM-*` suites (RFC 6209) that curl's OpenSSL builds offer. ADR-0118 requires
every hand-built primitive to be constant-time unless an ADR names it an exception, and
every type to say in its XML documentation which it is.

ARIA's substitution layers pass each of a block's 16 key-mixed bytes through one of four
fixed 256-byte S-boxes, which RFC 5794 specifies as tables: 192 to 256 look-ups a block
(12 to 16 rounds, plus the key schedule's three). Looking them up without a
data-dependent address means scanning each 256-byte table with masks, some hundred times
the cost of a direct look-up, or bitslicing the GF(2^8) inversion and affine maps behind
SB1 and SB2, which RFC 5794 does not describe. OpenSSL's own ARIA (`crypto/aria/aria.c`),
which curl uses when it negotiates these suites, indexes tables directly. curl's defaults
prefer AES-GCM and ChaCha20-Poly1305, so an ARIA suite is used only when a server chooses
one.

GCM's GHASH is a GF(2^128) multiplication, commonly done with 4-bit or 8-bit tables
indexed by the hash subkey H. It can instead be done bit by bit with masks, 128 shifts
and XORs a block, which keeps even a 16 KB TLS record to around a millisecond.

## Decision

- `Aria` in `Curl.Cryptography.UnitLibrary` indexes RFC 5794's SB1 to SB4 directly and
  says in its XML documentation that it is not constant-time. ADR-0118's list of types
  that are not constant-time becomes Blowfish, CAST-128, RC4, Camellia and ARIA.
- GCM is built once, in the internal `GaloisCounterMode` over an internal `IBlockCipher`,
  and its GHASH multiplication is constant-time: all 128 bits, selected with masks, no
  table and no branch on H or the data. `AeadAriaGcm` runs it over `Aria`, and the same
  code is checked against the BCL's `AesGcm` by running it over the BCL's AES.
- GCM takes the 12-byte nonce TLS uses (J0 = nonce || 00000001), as the BCL's `AesGcm`
  does, and a 16-byte tag.

## Consequences

- ARIA-GCM runs at table speed for the cipher, like the curl build it replaces.
- A local attacker who can observe the cache of the machine running Curl may learn key
  bits of an ARIA connection, as they could of OpenSSL's ARIA; GHASH leaks nothing more.
- A constant-time ARIA can replace this one behind the same public type later, and any
  other 16-byte block cipher that needs GCM can reuse `GaloisCounterMode`.

## Alternatives considered

- **Masked full-table scans for ARIA.** Constant-time, but about a hundred times slower
  for a suite curl's defaults never pick first.
- **Table-driven GHASH (Shoup's 4-bit tables).** Faster, but the tables are indexed by
  secret data and H, which the bitwise form avoids at an affordable cost.
