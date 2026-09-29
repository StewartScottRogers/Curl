# ADR-0145 — Camellia indexes its fixed S-boxes and is not constant-time

- **Status:** Accepted
- **Date:** 2026-09-28

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-783.
Amends ADR-0118's "Constant time and zeroing" section.

## Context

ADR-0140 adds Camellia (RFC 3713) to ADR-0118's list for the TLS Camellia-CBC suites
(RFC 5932) that curl's LibreSSL and OpenSSL builds offer. ADR-0118 requires every
hand-built primitive to be constant-time, with three named exceptions (Blowfish, CAST-128
and RC4), and every type to say in its XML documentation which it is.

Camellia's F-function passes each of the eight bytes of the key-mixed half block through
one of four fixed 256-byte S-boxes. RFC 3713 specifies them as tables. The ways to look
them up without a data-dependent address are:

- scan the whole table with masks for each look-up: 192 look-ups a block (8 per round,
  24 rounds for a 192- or 256-bit key) of 32 masked 64-bit reads each, some hundred
  times the cost of a direct look-up, which puts a Camellia-CBC download at a few
  megabytes a second;
- compute the S-box as the GF(2^8) inversion and affine maps of the Camellia
  specification paper, which RFC 3713 does not describe, bitsliced for speed.

OpenSSL's and LibreSSL's own Camellia, which curl uses when it negotiates these suites,
index the tables directly (`camellia.c`, and the scalar `camellia-x86_64.pl`). curl's
defaults prefer AES-GCM and ChaCha20-Poly1305, so a Camellia suite is used only when a
server chooses one.

## Decision

`Camellia` in `Curl.Cryptography.UnitLibrary` indexes RFC 3713's `SBOX1` directly, with
`SBOX2`, `SBOX3` and `SBOX4` derived from it by rotation as the RFC defines them. It is
not constant-time, and its XML documentation says so. ADR-0118's list of types that are
not constant-time becomes Blowfish, CAST-128, RC4 and Camellia.

Unlike Blowfish's, Camellia's tables are not key-dependent: what can leak through cache
timing is the key XOR data index of each look-up, the same exposure table-based AES and
the TLS libraries curl links have. The key schedule is still zeroed on `Dispose`, and
`stackalloc` temporaries in a `finally` block, as ADR-0118 requires.

## Consequences

- Camellia-CBC runs at table speed, like the curl builds it replaces.
- A local attacker who can observe the cache of the machine running Curl may learn key
  bits of a Camellia connection, as they could of OpenSSL's scalar Camellia.
- A constant-time Camellia (the bitsliced S-box) can replace this one behind the same
  public type later without changing a caller.
