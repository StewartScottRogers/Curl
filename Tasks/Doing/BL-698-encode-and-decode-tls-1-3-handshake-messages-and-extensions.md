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
completed:
---
# BL-698 — Encode and decode TLS 1.3 handshake messages and extensions

## Goal

`Curl.Tls.UnitLibrary` encodes ClientHello and decodes ServerHello (and HelloRetryRequest), EncryptedExtensions, CertificateRequest, Certificate, CertificateVerify, Finished and NewSessionTicket, with the extensions BL-695's ADR lists (at least `server_name`, `supported_groups`, `signature_algorithms`, `key_share`, `supported_versions`, `psk_key_exchange_modes`, `pre_shared_key`, `early_data`, `application_layer_protocol_negotiation`, `status_request`, `cookie`, `quic_transport_parameters`), per RFC 8446 section 4.

## Context

- Design: BL-695's ADR (the ClientHello it captured from curl's official builds is the model for the default extension order). Pure code: bytes in, typed records out and back; a malformed message is a typed `decode_error`, never an exception from an out-of-range read.
- Reference bytes: RFC 8448 section 3 (ClientHello, ServerHello, EncryptedExtensions, Certificate, CertificateVerify, Finished, NewSessionTicket).

## Acceptance criteria

- [ ] `Curl.Tls.UnitTests` reproduce RFC 8448 section 3's ClientHello bytes from its fields and decode every server message in that trace, round-trip each listed extension, and reject truncated lengths, duplicate extensions and an unknown message type with the typed alert.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Tls.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
