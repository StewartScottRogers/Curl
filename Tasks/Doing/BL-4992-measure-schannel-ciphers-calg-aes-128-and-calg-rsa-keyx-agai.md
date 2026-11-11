---
id: BL-4992
title: Measure Schannel --ciphers CALG_AES_128 and CALG_RSA_KEYX against a local TLS 1.2 server
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-4964]
touches: [Documentation/Decisions]
requirement: none
created: 2026-11-11
completed:
---
# BL-4992 — Measure Schannel --ciphers CALG_AES_128 and CALG_RSA_KEYX against a local TLS 1.2 server

## Goal

Measure curl 8.22.0 Schannel and Curl with `--ciphers CALG_AES_128` and `CALG_RSA_KEYX` against a local TLS 1.2 server and record the result.

## Context

Follow-up of BL-4964 criterion 2 and ADR-1245.

## Acceptance criteria

- [x] Both lists measured against a local TLS 1.2 server for curl 8.22.0 and Curl; result recorded in ADR-1250.

## Notes

- Measured with a C# file-based `SslStream` stand-in (TLS 1.2, prints the negotiated suite): REF and Curl agree for both lists (ECDHE-RSA-AES128-GCM-SHA256; TLS_RSA_WITH_AES_256_GCM_SHA384), exit 0.
- The exit 35 case needs a server refusing RSA key exchange, impossible with `SslStream` on Windows; not re-measured (ADR-1250).
- Found: REF's verbose WARNING line is missing in Curl; filed BL-4993.

## Log

- 2026-11-11: Created.
- 2026-11-11: Backlog -> Doing. Criteria at claim: fbecace0.
