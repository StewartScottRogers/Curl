---
id: BL-698
title: Encode and decode TLS 1.3 handshake messages and extensions
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-696]
touches: [Curl.Tls.UnitLibrary, Curl.Tls.UnitTests]
requirement: none
created: 2026-09-28
completed: 2026-09-28
---
# BL-698 — Encode and decode TLS 1.3 handshake messages and extensions

## Goal

`Curl.Tls.UnitLibrary` encodes ClientHello and decodes ServerHello (and HelloRetryRequest), EncryptedExtensions, CertificateRequest, Certificate, CertificateVerify, Finished and NewSessionTicket, with the extensions BL-695's ADR lists (at least `server_name`, `supported_groups`, `signature_algorithms`, `key_share`, `supported_versions`, `psk_key_exchange_modes`, `pre_shared_key`, `early_data`, `application_layer_protocol_negotiation`, `status_request`, `cookie`, `quic_transport_parameters`), per RFC 8446 section 4.

## Context

- Design: BL-695's ADR (the ClientHello it captured from curl's official builds is the model for the default extension order). Pure code: bytes in, typed records out and back; a malformed message is a typed `decode_error`, never an exception from an out-of-range read.
- Reference bytes: RFC 8448 section 3 (ClientHello, ServerHello, EncryptedExtensions, Certificate, CertificateVerify, Finished, NewSessionTicket).

## Acceptance criteria

- [x] `Curl.Tls.UnitTests` reproduce RFC 8448 section 3's ClientHello bytes from its fields and decode every server message in that trace, round-trip each listed extension, and reject truncated lengths, duplicate extensions and an unknown message type with the typed alert.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Tls.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

- Plan: a sticky-failure `TlsReader` (the first out-of-range read records `decode_error`,
  later reads return zero or empty, and vectors share the failure), so every decoder
  reads in a straight line and checks once in `Finish`; decoders return
  `TlsDecodeResult<T>` (value or `TlsAlertDescription`), never throw. `TlsWriter` fills in
  vector lengths after the body and throws `ArgumentException` for a body too long for
  its length field (a caller bug, not peer input).
- Built: `HandshakeMessageReader` (framing; `NeedMoreBytes` for a partial header or body),
  `ClientHello`, `ServerHello` (`IsHelloRetryRequest`), `EncryptedExtensions`,
  `CertificateRequest`, `CertificateMessage`/`CertificateEntry`, `CertificateVerify`,
  `Finished`, `NewSessionTicket`, each with `Encode()` (header included, as the
  transcript needs) and `Decode(body)`. Extension codecs for every extension listed plus
  `record_size_limit`, `padding` and `renegotiation_info`, which RFC 8448's hellos carry.
- Tests replay RFC 8448 sections 3 (all messages), 4 (resumption ClientHello with
  `pre_shared_key` and binder, ServerHello selecting identity 0), 5 (HelloRetryRequest
  with cookie) and 6 (CertificateRequest), each decoding to the trace's values and
  re-encoding to the same bytes. Every prefix of every message body is checked to give
  `decode_error`.
- Alert choices (sensible defaults, matching OpenSSL, which the Linux/macOS curl uses):
  an unknown handshake type is `unexpected_message` (OpenSSL `SSL_AD_UNEXPECTED_MESSAGE`);
  a repeated extension in one block is `illegal_parameter` (OpenSSL's
  `tls_collect_extensions`); a `server_name` entry that is not `host_name`, a
  `status_request` type other than `ocsp`, and non-zero `padding` are `illegal_parameter`.
  The first failure recorded wins.
- Host names and ALPN protocol names are Latin-1 strings, so any byte round-trips.
- `ServerHello.Decode` requires the extension block, as TLS 1.3 does; a TLS 1.2 ServerHello
  may omit it, which BL-703 handles when it builds the TLS 1.2 handshake.
- The three `ClientHelloProfile`s ADR-0140 assigns to BL-698 were split into BL-787 (with
  the remaining hello extensions: `ec_point_formats`, `session_ticket`,
  `post_handshake_auth`, `extended_master_secret`, `encrypt_then_mac`,
  `compress_certificate`, `certificate_authorities`) so this task stayed one run and its
  acceptance criteria unchanged.
- Results: 76 tests in `Curl.Tls.UnitTests`; Measure-CodeQuality: 100% line, 100% branch,
  133 members, 0 failing, worst CRAP 6. Solution build `-warnaserror` clean; fast tests
  green across 24 test assemblies.

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
- 2026-09-28: Doing -> Done. Curl.Tls encodes and decodes the TLS 1.3 handshake messages and extensions, replaying RFC 8448 sections 3-6 byte for byte
