# ADR-0219 — TLS 1.2 ECDHE agrees x448 and brainpool only when offered, and verifies brainpool ECDSA by hand

- **Status:** Accepted
- **Date:** 2026-09-29

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-803.

## Context

ADR-0140 lists x448 (`0x001e`) and brainpoolP256r1, brainpoolP384r1 and brainpoolP512r1
(`0x001a` to `0x001c`, RFC 7027) among the groups the hand-built TLS client supports.
ADR-0154 left them out of `Tls12ClientHandshake` until their primitives existed; BL-740
built X448 and BL-742 (ADR-0217) brainpool ECDH and ECDSA in
`Curl.Cryptography.UnitLibrary`. BL-803 wires them into TLS 1.2 ECDHE. It left open
whether the new groups join the default `supported_groups`, how a brainpool certificate
key is checked, and which alert each new failure sends.

## Decision

- **Opt-in groups.** `Tls12ClientSettings.SupportedGroups` accepts the four new codes
  (`TlsNamedGroup.IsTls12EcdheGroup`), but its default stays X25519 and the NIST curves,
  as ADR-0154 set it. Neither OpenSSL 3's nor Schannel's default list has a brainpool
  curve, so offering them by default would move the hello away from the platform's curl.
  Which groups a given build offers by default (OpenSSL 3.5's measured hello in
  `ClientHelloProfile` does include x448) is the caller's to set from that profile or from
  `--curves`, not this settings record's default.
- **Key shares.** x448 reuses `X448KeyShare`. The brainpool curves get
  `BrainpoolKeyShare` over `BrainpoolEcdh`: the uncompressed point out, the X coordinate
  as the pre-master secret. `SystemTlsRandomSource.CreateKeyShare` makes both.
  `TlsNamedGroup.CanShare`, the TLS 1.3 list, is unchanged: the TLS 1.3 brainpool groups
  are other code points (`0x001f` to `0x0021`, RFC 8734), not this task.
- **Signatures.** In TLS 1.2 a brainpool certificate key signs with the ordinary
  `ecdsa_sha256`/`sha384`/`sha512` codes (and `ecdsa_sha1`), since TLS 1.2 binds those
  to a hash, not a curve; at TLS 1.0 and 1.1, ECDSA over SHA-1. `TlsCertificatePublicKey`
  sends a key whose curve OID is a brainpool one (`1.3.36.3.3.2.8.1.1.7`, `.11`, `.13`)
  to `BrainpoolEcdsa.VerifyHash` with the DER `ECDSA-Sig-Value` decoded to r || s, on every
  platform, rather than to the BCL's `ECDsa`, which has no brainpool curve on macOS.
- **Alerts, as ADR-0154 names them.** A server point that is not on the chosen brainpool
  curve, or an x448 value giving an all-zero shared secret, is `illegal_parameter`, like
  every other bad ECDHE point. A TLS 1.3-only `ecdsa_brainpoolP*r1tls13_sha*` scheme
  (`0x081a` to `0x081c`) in a TLS 1.2 ServerKeyExchange is a scheme not offered:
  `illegal_parameter`. A brainpool certificate key that is not an uncompressed point of
  the curve's length is `bad_certificate`, as a key the BCL cannot import is; a signature
  that does not decode or does not match is `decrypt_error`.

## Consequences

- `Tls12ClientHandshake` completes ECDHE on x448 and the three brainpool curves, and
  verifies a brainpool ECDSA ServerKeyExchange, identically on Windows, Linux and macOS.
- A brainpool key of the right length whose point is off the curve fails as
  `decrypt_error` (the signature cannot verify) rather than `bad_certificate`; the key
  was never usable, and either alert ends the handshake.
- The client cannot yet present a brainpool client certificate: there is no brainpool
  `TlsSigningKey`. The test server signs with `BrainpoolEcdsa` in test code.

## Alternatives considered

- **Offer x448 and brainpool by default.** Lost: no platform's curl offers brainpool by
  default, and the default list is ADR-0154's, not this task's, to change.
- **Import brainpool keys through the BCL's `ECDsa`.** Lost: macOS has no brainpool
  curves, so the same certificate would verify on two platforms and fail on the third.
