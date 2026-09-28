---
id: BL-691
title: Build and verify GSS-API Kerberos context, wrap and MIC tokens
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-690]
touches: [Curl.Kerberos.UnitLibrary, Curl.Kerberos.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-691 — Build and verify GSS-API Kerberos context, wrap and MIC tokens

## Goal

`Curl.Kerberos.UnitLibrary` implements the GSS-API Kerberos V5 mechanism for an initiator: the initial context token (AP-REQ with the RFC 4121 authenticator checksum, flags for mutual authentication, delegation and integrity/confidentiality), verifying the acceptor's AP-REP, and the per-message Wrap and MIC tokens (RFC 4121 section 4.2; the RFC 1964 formats for `rc4-hmac` per RFC 4757), behind the token-source seam BL-525's ADR names.

## Context

- Standing rule (root `CLAUDE.md`, "Decisions", 2026-09-28). Builds on BL-690 (service ticket). Consumers: SPNEGO/Negotiate (BL-692, BL-527), SASL `GSSAPI` with its security-layer message (RFC 4752, BL-538), SOCKS5 GSS-API with RFC 1961 message protection (BL-615), FTP `--krb` with RFC 2228 protection (BL-693), and `--delegation` (BL-631: forwarded TGT in the checksum's `KRB_CRED`).
- RFC 2743 (token framing with the mechanism OID `1.2.840.113554.1.2.2`), RFC 4121 (checksum, sequence numbers, subkeys, `acceptor subkey` rules), RFC 4757 section 7 for `rc4-hmac` tokens.
- Tests drive an in-memory acceptor with fixed keys and an injected random source and time.

## Acceptance criteria

- [ ] `Curl.Kerberos.UnitTests` complete a mutual-auth context with the fake acceptor for an AES enctype and for `rc4-hmac`, pin the checksum flags for each `--delegation` level, and round-trip Wrap (with and without confidentiality) and MIC tokens in both directions, rejecting a bad sequence number and a tampered token with typed failures.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Kerberos.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

## Log

- 2026-09-28: Created.
