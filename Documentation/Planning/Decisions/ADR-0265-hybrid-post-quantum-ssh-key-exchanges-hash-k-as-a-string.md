# ADR-0265 — Hybrid post-quantum SSH key exchanges hash K as a string

- **Status:** Accepted
- **Date:** 2026-09-29

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-748.

## Context

ADR-0122 puts five hybrid post-quantum key exchanges at the head of the `Full` preset's
key-exchange list, as libssh 0.12.2 offers them: `mlkem768x25519-sha256`,
`mlkem768nistp256-sha256`, `mlkem1024nistp384-sha384`, `sntrup761x25519-sha512` and
`sntrup761x25519-sha512@openssh.com`. Neither reference build (libssh2 1.11.1) offers
them, so real curl on this machine cannot be measured running one; the wire format comes
from the specifications and the implementations libssh and OpenSSH interoperate with:
draft-ietf-sshm-mlkem-hybrid-kex, draft-josefsson-ntruprime-ssh, and OpenSSH's
`kexmlkem768x25519.c` and `kexsntrup761x25519.c`.

Three details differ from the classical exchanges and had to be pinned:

1. **How K enters H and the key derivation.** Every classical method hashes K as an
   `mpint`. The hybrid methods hash it as a `string`: OpenSSH builds the shared-secret
   buffer with `sshbuf_put_string` of the combined hash, and the drafts say the same.
2. **The order of the halves.** The client's `SSH_MSG_KEX_ECDH_INIT` carries one `string`,
   the KEM's public key followed by the classical public key; the server's reply carries
   the KEM ciphertext followed by its classical public key. K = HASH(KEM secret ||
   classical secret), with the method's hash.
3. **The classical secret's form.** It is the raw agreement: the 32 X25519 bytes (an
   all-zero result still refused, as OpenSSH's `kexc25519_shared_key_ext` does), or the
   fixed-length x-coordinate of the NIST-curve product - not an `mpint` of either.

## Decision

- `SshKeyExchangeOutcome` carries K already encoded (`EncodedSharedSecret`), and
  `SshKeyDerivation` hashes those bytes as they are. The classical methods encode with
  `SshExchangeHashInput.EncodeMpint`, the hybrid ones with `EncodeString`, so the
  transport and the key derivation never need to know which kind of method ran.
- One `HybridKemSshKeyExchange` runs all five, built from two `ISshKeyShare`s: the
  post-quantum share (`MlKemSshKeyShare` over `MlKem`, `Sntrup761SshKeyShare` over
  `Sntrup761`) and the classical share (`X25519SshKeyShare`, `NistCurveSshKeyShare` over
  the BCL's `ECDiffieHellman`). The classical `curve25519-sha256` and `ecdh-sha2-*`
  methods use the same classical shares, so the X25519 all-zero check and the on-curve
  check live in one place each.
- The server's share must be exactly the ciphertext length plus the classical key
  length (ML-KEM-768 1088 + 32 or + 65, ML-KEM-1024 1568 + 97, sntrup761 1039 + 32); any
  other length, an unusable classical key, or a signature that no longer verifies fails
  the key exchange as every other malformed exchange does: exit 2, `Failure establishing
  ssh session: -8, Unable to exchange encryption keys` (ADR-0122, measured in BL-564).
  A tampered KEM ciphertext is not detected by decapsulation - both KEMs reject
  implicitly - but changes K and H, so the signature check refuses it.
- `ISshEphemeralKeySource` gains `CreateMlKemKey` and `CreateSntrup761KeyPair`, so tests
  fix the client's KEM keys as they already fix its X25519 and NIST keys.

## Consequences

- With the `Full` preset every key-exchange name ADR-0122 lists is implemented; the
  catalogue no longer filters any of them out.
- The tests compute each hybrid exchange's H and keys independently in
  `TestKeyExchangeServer` and check the transport reaches the same values; no captured
  exchange with a real libssh or OpenSSH peer pins them yet.
