# ADR-0259 — chacha20-poly1305@openssh.com packets pad to eight without the length, and a failed tag is libssh2's -12

- **Status:** Accepted
- **Date:** 2026-09-29

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-679.
Builds on ADR-0122 (the algorithm lists) and ADR-0212 (the `ISshPacketProtection` seam and
the codes a failed check ends with).

## Context

BL-679 adds `chacha20-poly1305@openssh.com`, the first cipher in both reference builds'
`KEXINIT` lists, as one more `ISshPacketProtection` over `Curl.Cryptography.UnitLibrary`'s
`ChaCha20` and `Poly1305`. OpenSSH's `PROTOCOL.chacha20poly1305` fixes the construction; it
leaves two things for this library to choose: how packets are padded, and what curl prints
when a packet fails its tag.

## Decision

- **The construction is OpenSSH's, exactly.** The 64-byte encryption key (`C` or `D`,
  derived to 64 bytes; no IV is derived) is `K_2` (bytes 0-31) then `K_1` (bytes 32-63).
  The nonce is the sequence number as a 64-bit big-endian integer, with ChaCha's original
  64-bit block counter. `packet_length` is encrypted under `K_1` from block 0; the
  Poly1305 key is the first 32 bytes of block 0 under `K_2`; the rest of the packet is
  encrypted under `K_2` from block 1; the 16-byte tag covers the encrypted length and the
  encrypted rest. No MAC is negotiated beside it (already `IsAuthenticatedEncryption`).
- **The tag is checked before anything but the length is decrypted**, with `Poly1305.Verify`
  (constant-time). The length has to be decrypted first to know where the tag is.
- **Block size 8, and the four length bytes are not padded**: OpenSSH's `packet.c` pads
  and checks `packet_length` alone to the block size whenever the cipher has an
  authentication tag, as for AES-GCM, and libssh2 1.11.1 interoperates with it.
- **A failed tag is `LIBSSH2_ERROR_DECRYPT`, -12**, the AES-GCM code: libssh2 1.11.1's
  `decrypt()` returns it whenever an integrated-MAC cipher refuses a packet. Not measured:
  the Windows reference build offers the name but reaching it needs a server built from
  these classes, as ADR-0212's run was. BL-897 (2026-09-30) confirmed -12 for AES-GCM on the
  OpenSSL build, but there a chacha20-poly1305 packet with its tag or ciphertext altered
  left curl printing nothing and not exiting; BL-1032 measures that case.
- **A corrupted length** decrypts to a different `packet_length`. One off the block size or
  over the maximum is the framing failure ADR-0206 maps (`-8`, exit 2); one that happens to
  frame fails its tag (`-12`) or runs out of bytes, as the peer would see it.

## Consequences

- Against an OpenSSH server both presets now agree `chacha20-poly1305@openssh.com`, their
  first choice, where they agreed AES-GCM (OpenSSL) or AES-CTR with HMAC-SHA-256 (Windows).
- The hand-built ChaCha20 is slower than the BCL's AES; a transfer is bound by the
  connection, not the cipher, at the sizes curl moves.

## Alternatives considered

- **Pad including the length, as RFC 4253 frames packets.** OpenSSH would refuse such a
  packet as off the block size; rejected.
- **The BCL's `ChaCha20Poly1305`.** It is RFC 8439's AEAD, with a 12-byte nonce, a
  length-padded MAC input and no separate length key; OpenSSH's construction cannot be
  built from it. Rejected for the raw primitives BL-673 provides.
