---
id: BL-913
title: Pass the HTTPS server certificate's tls-server-end-point bindings to the hand-built Kerberos Negotiate
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-832]
touches: [Curl.Authentication.UnitLibrary, Curl.Authentication.UnitTests]
requirement: none
created: 2026-09-29
completed:
---
# BL-913 — Pass the HTTPS server certificate's tls-server-end-point bindings to the hand-built Kerberos Negotiate

## Goal

`HandBuiltKerberosSecurityContext` sets `KerberosGssContextOptions.ChannelBindings` to `tls-server-end-point:` followed by the HTTPS server certificate's RFC 5929 hash (SHA-256 for an MD5 or SHA-1 signature, else the signature's own hash), so hand-built Negotiate over HTTPS sends the bindings curl 8.18.0 with MIT was measured sending (BL-832, ADR-0171).

## Context

- BL-832 measured curl 8.18.0 with MIT krb5 1.22.1 over HTTPS: its authenticator's `Bnd` is the MD5 of RFC 2744's structure with no addresses and application data `tls-server-end-point:` + SHA-256 of the sha256RSA server certificate. `Curl.Kerberos.UnitLibrary` now takes that application data (`KerberosGssContextOptions.ChannelBindings`).
- `Curl.Authentication.UnitLibrary/HandBuiltKerberosSecurityContext.cs` builds the options today without it. The server certificate has to reach it through the security-context request; plain HTTP passes none (zeros), as curl does.
- If the certificate cannot reach the request without changing another project, add that project to `touches` per the board's rules.

## Acceptance criteria

- [ ] A `Curl.Authentication.UnitTests` test pins the options' `ChannelBindings` for a known certificate (a sha256RSA one gives `tls-server-end-point:` + its SHA-256).
- [ ] A test pins that a request without a server certificate (plain HTTP) passes `null`.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Authentication.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

- Filed by BL-832.

## Log

- 2026-09-29: Created.
