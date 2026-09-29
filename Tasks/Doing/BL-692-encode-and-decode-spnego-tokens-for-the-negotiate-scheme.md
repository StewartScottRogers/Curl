---
id: BL-692
title: Encode and decode SPNEGO tokens for the Negotiate scheme
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-525]
touches: [Curl.Authentication.UnitLibrary, Curl.Authentication.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-692 — Encode and decode SPNEGO tokens for the Negotiate scheme

## Goal

`Curl.Authentication.UnitLibrary` encodes SPNEGO NegTokenInit (with the mechanism list curl's build offers: Kerberos V5, the legacy Microsoft Kerberos OID, NTLMSSP, in its order) and decodes NegTokenResp (negState, supportedMech, responseToken, mechListMIC), per RFC 4178, with `System.Formats.Asn1`, so the Negotiate authenticator (BL-527) can carry either a Kerberos (BL-691) or an NTLM (BL-683) token.

## Context

- Standing rule (root `CLAUDE.md`, "Decisions", 2026-09-28): Negotiate works on every platform. Design and seam: BL-525's ADR. SPNEGO is the Negotiate scheme's own wrapping (RFC 4559 for HTTP), so it lives with the Negotiate authenticator rather than in the Kerberos or NTLM library.
- RFC 4178 section 4.2 (NegotiationToken), RFC 2743 section 3.1 (the initial-token framing with OID `1.3.6.1.5.5.2`). The mechanism list and order: record them from a real curl `--negotiate` exchange (Windows SSPI and Linux GSS-API) through `Record-CurlExchange.ps1` with a `401 Negotiate` server, into Notes.
- Pure code; a malformed token is a typed failure.

## Acceptance criteria

- [ ] Measured first as above; the NegTokenInit bytes curl sent copied into Notes per platform.
- [ ] `Curl.Authentication.UnitTests` pin the NegTokenInit bytes for a fixed inner token and each mechanism list, decode accept-completed, accept-incomplete and reject NegTokenResp examples, and reject malformed tokens with the typed failure.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Authentication.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
