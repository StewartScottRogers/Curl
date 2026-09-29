---
id: BL-786
title: Decompress server certificates to RFC 8879 in the hand-built TLS client
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-699, BL-785, BL-860]
touches: [Curl.Tls.UnitLibrary, Curl.Tls.UnitTests, Documentation/Planning/Decisions]
requirement: none
created: 2026-09-28
completed: 2026-09-29
---
# BL-786 — Decompress server certificates to RFC 8879 in the hand-built TLS client

## Goal

The hand-built TLS 1.3 client offers `compress_certificate` (27) with the algorithms its ClientHello profile lists (the OpenSSL profile: zlib, brotli, zstd) and accepts a `CompressedCertificate` (handshake type 25) in place of `Certificate`, decompressing it before the chain goes to `IServerCertificateVerifier`.

## Context

- ADR-0140 (BL-695): the measured OpenSSL 3.5.5 ClientHello carries `compress_certificate` `0400010003`; the hand-built client supports every extension an official build offers.
- RFC 8879: the uncompressed length is checked against the declared `uncompressed_length` and a limit (2^24); a mismatch or a decompression failure is a `bad_certificate` alert. zlib through the BCL's `ZLibStream`, Brotli through `BrotliDecoder`, Zstandard through `Curl.Zstandard.UnitLibrary`'s `ZstandardDecoder.TryDecompress` into a buffer of the declared length (ADR-0185, built by BL-857 to BL-860; add that reference here, as ADR-0120 amended by ADR-0185 allows).
- Builds on BL-699 (TLS 1.3 handshake) and BL-698 (codecs).

## Acceptance criteria

- [x] The ClientHello built from `ClientHelloProfile.OpenSsl` carries `compress_certificate` pinned against ADR-0140's captured bytes - which are `0001 0003` (zlib, zstd), not the `0001 0002 0003` first written here; see Notes and ADR-0197. (`ClientHelloProfileTests.OpenSslProfileOffersCertificateCompressionAsCaptured`, `Tls13CertificateCompressionHandshakeTests.TheClientHelloOffersTheListedAlgorithmsAfterKeyShare`.)
- [x] A test server sending a `CompressedCertificate` with each algorithm completes the handshake and the verifier receives the same DER chain as with an uncompressed `Certificate`. (`ACompressedCertificateWithEachAlgorithmCompletesWithTheSameChain`, zlib, brotli, zstd.)
- [x] A wrong `uncompressed_length`, an algorithm not offered, and corrupt compressed data each fail with a `bad_certificate` alert as a typed `TlsHandshakeFailure`. (`AWrongUncompressedLengthIsABadCertificate`, `AnAlgorithmNotOfferedIsABadCertificate`, `CorruptCompressedDataIsABadCertificate`.)
- [x] The library meets the quality gates. (Every `Curl.Tls.UnitLibrary` class at 100% line and branch coverage in the Cobertura run; build clean with CA1502 at 10.)

## Notes

- 2026-09-29, decision (ADR-0197): the criterion asked for `0001 0002 0003` *and* for
  ADR-0140's captured bytes, but the capture is `001b 0005 04 0001 0003`: Ubuntu's
  OpenSSL has no brotli. The measured bytes win, so `ClientHelloProfile.OpenSsl` keeps
  zlib and zstd; brotli is still decompressed when a caller offers it.
- Offered algorithms live in `Tls13ClientSettings.CertificateCompressionAlgorithms`
  (default none); `compress_certificate` joins `DefaultExtensionOrder` after `key_share`,
  so a default hello is unchanged. A CompressedCertificate with nothing offered is
  `unexpected_message`; a truncated one is `decode_error`.
- Decompression writes into a buffer one byte past the declared length, so a stream that
  is longer or shorter than declared fails in one pass. The CompressedCertificate as sent
  enters the transcript (RFC 8879 section 4).
- The test server's zstd "compressor" writes raw blocks: the BCL has no Zstandard encoder
  and the decoder under test reads any valid frame.
- `touches` gained `Documentation/Planning/Decisions` for ADR-0197 and its index row; no
  task in Doing names it.
- Added the `Curl.Zstandard.UnitLibrary` reference ADR-0185 allowed;
  `ProtocolIsolationTests` already carried that row.

## Log

- 2026-09-28: Created.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Done. The hand-built TLS 1.3 client offers compress_certificate and decompresses zlib, brotli and zstd CompressedCertificates to RFC 8879
