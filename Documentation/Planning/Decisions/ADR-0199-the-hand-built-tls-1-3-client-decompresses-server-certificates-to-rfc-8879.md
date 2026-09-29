# ADR-0199 — The hand-built TLS 1.3 client decompresses server certificates to RFC 8879, offering what the measured hello offers

- **Status:** Accepted
- **Date:** 2026-09-29

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-786.
Builds on ADR-0140 (the hand-built TLS client and its measured ClientHellos) and ADR-0185
(the hand-built Zstandard decoder, which `Curl.Tls.UnitLibrary` may reference).

## Context

ADR-0140's OpenSSL 3.5.5 ClientHello (Ubuntu's curl 8.18.0) ends with
`compress_certificate` (27): `001b 0005 04 0001 0003`. BL-786 asked for the extension
"with `0001 0002 0003`" (zlib, brotli, zstd) *and* for it to be pinned against those
captured bytes. The two disagree: the captured bytes list zlib and zstd only, because
Ubuntu builds OpenSSL without brotli. A server that receives the extension may then send a
CompressedCertificate (handshake type 25) in place of its Certificate, and the client must
decompress it before the chain reaches `IServerCertificateVerifier`.

## Decision

- **The captured bytes win.** `ClientHelloProfile.OpenSsl` keeps `[zlib, zstd]`, pinned
  by `ClientHelloProfileTests.OpenSslProfileOffersCertificateCompressionAsCaptured`. The
  standing rule is to match the platform's curl as measured, and the measured curl does
  not offer brotli. The decoder still accepts brotli, so a caller that offers it gets it.
- **`Tls13ClientSettings.CertificateCompressionAlgorithms`** (default none) is what the
  ClientHello offers, in `compress_certificate` at its place in `ExtensionOrder`
  (`DefaultExtensionOrder` now lists it after `key_share`, as OpenSSL does). Only zlib (1),
  brotli (2) and zstd (3) are accepted (`CertificateCompressionAlgorithm.CanDecompress`);
  offering any with no place for the extension throws, as `RequestOcspStatus` does.
- **`CompressedCertificate`** is the message codec; `Decompress(offered)` returns the
  Certificate message body (without its handshake header, as OpenSSL and BoringSSL
  compress it) or nothing. zlib goes through the BCL's `ZLibStream`, brotli through
  `BrotliDecoder.TryDecompress`, zstd through `ZstandardDecoder.TryDecompress`, each into
  a buffer one byte longer than the declared `uncompressed_length`, so a stream that
  decompresses to more or less than it declares is caught without a second pass. The
  declared length is a `uint24`, so RFC 8879's 2^24 limit holds by construction.
- **Alerts.** An algorithm the client did not offer, a wrong `uncompressed_length` and data
  that does not decompress are each `bad_certificate` (RFC 8879 section 4), before the
  verifier sees anything. A malformed CompressedCertificate is `decode_error`, as every
  other codec's truncation is. A CompressedCertificate when the client offered no
  compression is `unexpected_message`: the message is not one the handshake can receive.
- **Transcript.** The CompressedCertificate as sent enters the transcript, not the
  decompressed Certificate (RFC 8879 section 4), so CertificateVerify and Finished are
  computed over what was on the wire.
- The client never compresses its own certificate: curl sends none compressed, and a
  CertificateRequest's `compress_certificate` is not read.

## Consequences

- A QUIC or TCP handshake built from the OpenSSL profile's list accepts the certificate
  compression real servers (Cloudflare, Google) send, and the verifier sees the same DER
  chain it would uncompressed.
- The Zstandard reference ADR-0185 allowed is now in place in `Curl.Tls.UnitLibrary`'s
  project file.
- Worst case memory is the declared length plus one byte, at most 16 MiB, for one message.

## Alternatives considered

- **Offer `0001 0002 0003` in the OpenSSL profile, as the task's criterion read.** Lost:
  it would no longer be the measured hello byte for byte, and ADR-0140's profiles exist
  to be that.
- **Decompress with a growing buffer and compare lengths afterwards.** Lost: the declared
  length is known up front, and a fixed buffer bounds the work a lying server can cause.
