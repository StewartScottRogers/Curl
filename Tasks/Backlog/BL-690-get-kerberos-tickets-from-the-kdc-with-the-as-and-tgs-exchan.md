---
id: BL-690
title: Get Kerberos tickets from the KDC with the AS and TGS exchanges
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-686, BL-687, BL-688, BL-689]
touches: [Curl.Kerberos.UnitLibrary, Curl.Kerberos.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-690 — Get Kerberos tickets from the KDC with the AS and TGS exchanges

## Goal

`Curl.Kerberos.UnitLibrary` gets a service ticket for a service principal: from the credential cache when one is there, otherwise by a TGS exchange with the cached TGT, and (where BL-525's ADR says curl's platform build does, for example SSPI with `-u user:password`) by an AS exchange with a password and PA-ENC-TIMESTAMP pre-authentication, over an injected KDC transport, with every KRB-ERROR turned into a typed failure.

## Context

- Standing rule (root `CLAUDE.md`, "Decisions", 2026-09-28). Builds on BL-686 (enctypes), BL-687 (messages), BL-688 (cache), BL-689 (configuration and KDC location).
- RFC 4120 sections 3.1 (AS exchange), 3.3 (TGS exchange), 7.2.1 (UDP first, TCP with the 4-byte length prefix, switch to TCP on `KRB_ERR_RESPONSE_TOO_BIG`), 5.9.1 (error codes). Clock skew and nonces use `TimeProvider` and an injected random source.
- The KDC transport is an interface in this library (send request bytes to a KDC endpoint over UDP or TCP, return the reply); its socket implementation lives in `Curl.Networking.UnitLibrary` and is composed by BL-527. Tests use an in-memory fake KDC built from BL-686/BL-687 with fixed keys.

## Acceptance criteria

- [ ] `Curl.Kerberos.UnitTests` obtain a service ticket from a cached TGT through the fake KDC, obtain a TGT with a password after a `KDC_ERR_PREAUTH_REQUIRED` round trip, fall back from UDP to TCP on `KRB_ERR_RESPONSE_TOO_BIG`, and map `KDC_ERR_S_PRINCIPAL_UNKNOWN`, `KDC_ERR_PREAUTH_FAILED` and a clock-skew error to typed failures.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Kerberos.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

## Log

- 2026-09-28: Created.
