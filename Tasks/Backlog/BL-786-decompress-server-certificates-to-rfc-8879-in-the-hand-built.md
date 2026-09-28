---
id: BL-786
title: Decompress server certificates to RFC 8879 in the hand-built TLS client
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-699, BL-785]
touches: [Curl.Tls.UnitLibrary, Curl.Tls.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-786 — Decompress server certificates to RFC 8879 in the hand-built TLS client

## Goal

The hand-built TLS 1.3 client offers `compress_certificate` (27) with the algorithms its ClientHello profile lists (the OpenSSL profile: zlib, brotli, zstd) and accepts a `CompressedCertificate` (handshake type 25) in place of `Certificate`, decompressing it before the chain goes to `IServerCertificateVerifier`.

## Context

- ADR-0140 (BL-695): the measured OpenSSL 3.5.5 ClientHello carries `compress_certificate` `0400010003`; the hand-built client supports every extension an official build offers.
- RFC 8879: the uncompressed length is checked against the declared `uncompressed_length` and a limit (2^24); a mismatch or a decompression failure is a `bad_certificate` alert. zlib through the BCL's `ZLibStream`, Brotli through `BrotliDecoder`, Zstandard through the decoder BL-785 places (add that reference here, as ADR-0120 amended by BL-785 allows).
- Builds on BL-699 (TLS 1.3 handshake) and BL-698 (codecs).

## Acceptance criteria

- [ ] The ClientHello built from `ClientHelloProfile.OpenSsl` carries `compress_certificate` with `0001 0002 0003`, pinned against ADR-0140's captured bytes.
- [ ] A test server sending a `CompressedCertificate` with each algorithm completes the handshake and the verifier receives the same DER chain as with an uncompressed `Certificate`.
- [ ] A wrong `uncompressed_length`, an algorithm not offered, and corrupt compressed data each fail with a `bad_certificate` alert as a typed `TlsHandshakeFailure`.
- [ ] The library meets the quality gates.

## Notes

## Log

- 2026-09-28: Created.
