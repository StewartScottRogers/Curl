# ADR-1111 — Oblivious HTTP encapsulation skips unknown KEMs and throws three typed exceptions

- **Status:** Accepted
- **Date:** 2026-10-29

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-4310.

## Context

BL-4310 asks `Curl.Extension.ObliviousHttp.UnitLibrary` to read a gateway's key configurations (RFC 9458 section 3,
served as RFC 9540 section 3 describes), choose the first one it supports, encapsulate a request (section 4.3) and
decapsulate the response (section 4.4), failing with typed exceptions. A key configuration does not carry its public
key's length: only the KEM says how long it is, so a configuration with an unknown KEM cannot be delimited on its own.

## Decision

- **Unknown KEMs.** `KeyConfiguration.Parse`, given one configuration, throws `UnsupportedKeyConfigurationException`
  when the KEM is unknown, since its public key cannot be found. In an `application/ohttp-keys` list every entry has
  its own length, so `KeyConfigurationList.Parse` leaves such an entry out and reads on: a gateway that adds a newer
  KEM still serves older clients.
- **Selection.** `SelectFirstSupported` walks configurations in the gateway's order and, within each, its KDF and AEAD
  pairs in order, and takes the first pair this library implements (HKDF-SHA256 with AES-128-GCM, AES-256-GCM or
  ChaCha20Poly1305). RFC 9458 leaves the choice to the client; the gateway's order is its preference.
- **Three exceptions.** Bytes that do not parse (truncated, left over, an empty or ragged suite list) throw
  `MalformedKeyConfigurationException : FormatException`; well-formed configurations with nothing usable throw
  `UnsupportedKeyConfigurationException : NotSupportedException`; a response shorter than its nonce and tag, or one
  whose tag does not match, throws `EncapsulatedResponseException : CryptographicException`. Each says what went
  wrong in the type a caller already catches for that kind of failure.
- **One object per exchange.** `ObliviousRequest` holds the HPKE sender context from encapsulation, because the
  response's keys derive from it (its exporter secret and encapsulated key); it is `IDisposable` like `HpkeSender`.
  The response AEAD uses the BCL's `AesGcm` and `HKDF` and `Curl.Cryptography`'s `AeadChaCha20Poly1305`, the same
  primitives `HpkeContext` uses.

## Consequences

RFC 9458 Appendix A is reproduced byte for byte, and BL-4137 can build the relay transfer on `KeyConfigurationList`
and `ObliviousRequest` without another HPKE layer.
