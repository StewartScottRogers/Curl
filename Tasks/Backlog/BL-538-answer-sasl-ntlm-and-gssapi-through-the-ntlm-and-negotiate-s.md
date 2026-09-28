---
id: BL-538
title: Answer SASL NTLM and GSSAPI through the NTLM and Negotiate seam
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-526, BL-527, BL-536]
touches: [Curl.Authentication.UnitLibrary, Curl.Authentication.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-538 — Answer SASL NTLM and GSSAPI through the NTLM and Negotiate seam

## Goal

The SASL authenticator answers the NTLM and GSSAPI mechanisms with the token source BL-525's ADR chose (built by BL-526 and BL-527), and ranks them as curl 8.21.0 does, so mail servers that offer only those work where the platform allows.

## Context

- Conformance audit 2026-09-28, rows 23 and 34. GSSAPI uses the service name `smtp`/`imap`/`pop` by default and `--service-name` overrides it (row 23's service-name task wires the option).
- RFC 4752 (GSSAPI SASL, including the security-layer negotiation message after the context completes). Platforms where BL-525's ADR says a mechanism is unavailable rank it as not offered.

## Acceptance criteria

- [ ] Measured first with `Record-CurlExchange.ps1 -Smtp` offering `AUTH NTLM` with a fixed `334` Type 2 challenge; the messages curl sent copied into Notes (GSSAPI cannot be measured without a KDC; say so in Notes).
- [ ] `Curl.Authentication.UnitTests` with a fake token source pin the NTLM SASL exchange and the GSSAPI exchange including the final security-layer message, and the ranking.
- [ ] Each platform's availability, as the ADR states it, is pinned in `OSCondition` tests.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Authentication.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

## Log

- 2026-09-28: Created.
