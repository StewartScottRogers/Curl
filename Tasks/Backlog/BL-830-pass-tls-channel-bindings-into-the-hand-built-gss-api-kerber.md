---
id: BL-830
title: Pass TLS channel bindings into the hand-built GSS-API Kerberos checksum
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-691]
touches: [Curl.Kerberos.UnitLibrary, Curl.Kerberos.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-830 — Pass TLS channel bindings into the hand-built GSS-API Kerberos checksum

## Goal

`KerberosGssContextOptions` takes optional channel bindings, and `KerberosGssContext` puts their MD5 (RFC 4121 section 4.1.1.2, over RFC 2744's `gss_channel_bindings_struct` encoding) in the authenticator checksum's `Bnd` field in place of the zeros it sends now, if measurement shows the platform curl's GSS-API Negotiate passes them.

## Context

- BL-691 sends all-zero bindings (ADR-0169, Consequences). Whether curl's GSS-API Negotiate over HTTPS passes `tls-server-end-point` bindings (RFC 5929) depends on the curl version; measure curl 8.21.0 with MIT on Linux against a loopback HTTPS server with `Record-CurlExchange.ps1` before pinning anything, and record the answer in an ADR (or amend ADR-0169).
- If curl passes none, record that and finish with the zeros kept.

## Acceptance criteria

- [ ] The ADR states whether curl 8.21.0 with MIT sends channel bindings, from a recorded exchange.
- [ ] When it does, `Curl.Kerberos.UnitTests` pin the `Bnd` field for a known `tls-server-end-point` value against a hand-computed MD5 of the RFC 2744 structure; when it does not, a test pins the zeros with the ADR cited.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Kerberos.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

- Filed by BL-691.

## Log

- 2026-09-28: Created.
