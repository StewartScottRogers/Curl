# ADR-0150 — TLS 1.0 CBC inserts OpenSSL's empty fragment, and CBC records fail bad padding and bad MACs alike

- **Status:** Accepted
- **Date:** 2026-09-28

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-702.

## Context

BL-702 builds the TLS 1.2, 1.1 and 1.0 record layer of the hand-built TLS client
(ADR-0140): the PRF, the key block, and record protection for every bulk cipher ADR-0140
lists. Three questions had more than one answer.

**The BEAST countermeasure.** TLS 1.0 CBC chains each record's IV from the last
ciphertext block of the one before, which BEAST exploits. Stacks answer it in two ways:
NSS, Schannel and Secure Transport split each write 1/n-1 (one byte, then the rest);
OpenSSL and LibreSSL send an empty application data record before each write.
OpenSSL 3.5's record layer (`ssl/record/methods/tls1_meth.c`, `tls1_initialise_write_packets`)
adds the empty prefix record when `need_empty_fragments` is set and the write is
application data; `tls1_set_crypto_state` sets it for a CBC cipher at TLS 1.0 and below
unless `SSL_OP_DONT_INSERT_EMPTY_FRAGMENTS` is set. `SSL_OP_ALL` sets that option, and
curl's `lib/vtls/openssl.c` clears it again unless `CURLSSLOPT_ALLOW_BEAST`
(`--ssl-allow-beast`) is given. The hand-built client runs over TCP only where
`SslStream` cannot, which for TLS 1.0 means matching curl.se's official build (LibreSSL,
OpenSSL's record layer) on Windows and the OpenSSL build elsewhere (ADR-0140). BL-702's
own wording and BL-713 say "1/n-1", which is Schannel's split, not the one the curl
builds Curl matches here make.

**Lucky Thirteen.** A MAC-then-encrypt CBC record must be rejected with
`bad_record_mac` whether its padding or its MAC is wrong, by one path, or a padding
oracle opens. OpenSSL goes further (`ssl3_cbc_digest_record`): it hashes a fixed number
of blocks whatever the padding, which needs direct access to the hash's compression
function. The BCL's hashes do not expose it, so doing the same means hand-building
SHA-1, SHA-256 and SHA-512's compression functions.

**GCM's explicit nonce.** RFC 5288 leaves the 8-byte explicit nonce to the sender as
long as it never repeats under one key. OpenSSL starts from a random value and counts
up; BoringSSL and Go send the sequence number.

## Decision

- `Tls12RecordWriteState` with a CBC cipher at TLS 1.0 writes one empty application
  data record before each call that writes non-empty application data, as OpenSSL and
  LibreSSL do, and `insertEmptyFragment: false` (for `--ssl-allow-beast`) turns it off.
  It never applies to TLS 1.1 or 1.2, to the null or AEAD ciphers, or to handshake,
  alert and change_cipher_spec records. BL-713 pins this split, not 1/n-1.
- Opening a MAC-then-encrypt CBC record checks the padding without a branch on the
  decrypted bytes (every one of the last 256 bytes is read and folded into a mask, as
  OpenSSL's `tls1_cbc_remove_padding_and_mac` does), always computes and compares the
  MAC in fixed time, and branches once, on both answers together. Bad padding, a bad MAC
  and a malformed length are all `bad_record_mac`. Since BL-795 the MAC is computed as
  OpenSSL's `ssl3_cbc_digest_record` does: `Curl.Cryptography`'s `FixedBlockHmac` runs
  hand-built SHA-1, SHA-256 and SHA-384 compression functions over the same number of
  blocks whatever the padding length, writing the `0x80` byte and the bit length with
  masks and picking the state after the true last block with a mask, and
  `Tls12RecordMac.VerifyInFixedBlocks` copies the received MAC out of the record with
  masks over every offset it could start at. The Lucky Thirteen residual the first
  version had (the hashing time following the padding length by a few blocks, as in
  Go's `crypto/tls`) is gone.
- Encrypt-then-MAC (RFC 7366) checks the MAC first, over the IV and ciphertext, and only
  then decrypts, so no padding oracle exists there.
- GCM's explicit nonce is the record's sequence number.

## Consequences

- TLS 1.0 CBC connections through the hand-built client send the same record sequence
  as curl's LibreSSL and OpenSSL builds, and `--ssl-allow-beast` changes it the same way.
- A padding oracle through error codes, through a branch, or through the MAC's hashing
  time is closed: opening a MAC-then-encrypt record takes the same number of
  compression-function calls whatever its padding. The price is that the last 256 bytes
  or so of each record are hashed byte by byte with masks. CBC suites are only reachable when a
  server prefers them over every AEAD suite Curl offers.
- The GCM nonce is deterministic, so a test can pin a record byte for byte.

## Alternatives considered

- **Split 1/n-1 as the task text said.** That is Schannel's behaviour, which Curl only
  matches where it already uses Schannel through `SslStream`; the builds this client
  stands in for insert an empty fragment.
- **Hand-build SHA compression now.** The complete fix, but it is a cryptographic
  primitive in its own right and belongs in `Curl.Cryptography.UnitLibrary` under its
  own task (BL-795), not inside the record layer.
- **A random starting GCM nonce, as OpenSSL.** Equally valid on the wire and harder to
  test, with nothing gained.
