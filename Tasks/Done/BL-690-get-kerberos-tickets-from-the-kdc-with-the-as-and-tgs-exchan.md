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
completed: 2026-09-28
---
# BL-690 — Get Kerberos tickets from the KDC with the AS and TGS exchanges

## Goal

`Curl.Kerberos.UnitLibrary` gets a service ticket for a service principal: from the credential cache when one is there, otherwise by a TGS exchange with the cached TGT, and (where BL-525's ADR says curl's platform build does, for example SSPI with `-u user:password`) by an AS exchange with a password and PA-ENC-TIMESTAMP pre-authentication, over an injected KDC transport, with every KRB-ERROR turned into a typed failure.

## Context

- Standing rule (root `CLAUDE.md`, "Decisions", 2026-09-28). Builds on BL-686 (enctypes), BL-687 (messages), BL-688 (cache), BL-689 (configuration and KDC location).
- RFC 4120 sections 3.1 (AS exchange), 3.3 (TGS exchange), 7.2.1 (UDP first, TCP with the 4-byte length prefix, switch to TCP on `KRB_ERR_RESPONSE_TOO_BIG`), 5.9.1 (error codes). Clock skew and nonces use `TimeProvider` and an injected random source.
- The KDC transport is an interface in this library (send request bytes to a KDC endpoint over UDP or TCP, return the reply); its socket implementation lives in `Curl.Networking.UnitLibrary` and is composed by BL-527. Tests use an in-memory fake KDC built from BL-686/BL-687 with fixed keys.

## Acceptance criteria

- [x] `Curl.Kerberos.UnitTests` obtain a service ticket from a cached TGT through the fake KDC, obtain a TGT with a password after a `KDC_ERR_PREAUTH_REQUIRED` round trip, fall back from UDP to TCP on `KRB_ERR_RESPONSE_TOO_BIG`, and map `KDC_ERR_S_PRINCIPAL_UNKNOWN`, `KDC_ERR_PREAUTH_FAILED` and a clock-skew error to typed failures.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Kerberos.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

- Plan (ADR-0164, decided by Claude under Stewart's delegation): `KerberosKdcClient` has two
  `GetServiceTicketAsync` entry points, one per credential source. A `CredentialCache` gives its own
  live service ticket, else a TGS exchange with its live `krbtgt/REALM@REALM`, else `NoCredentials`
  (MIT's `gss_init_sec_context`). A `KerberosPasswordCredential` gives an AS exchange then a TGS
  exchange and ignores the cache (SSPI's explicit-credential path). `GetInitialTicketAsync` and
  `GetTicketFromTicketGrantingServiceAsync` are public for the GSS-API mechanism.
- AS: first AS-REQ without pre-authentication; on `KDC_ERR_PREAUTH_REQUIRED`, `PA-ENC-TIMESTAMP` in
  the first `PA-ETYPE-INFO2` type this library has, with its salt and s2kparams (default salt and the
  first offered type when there is no hint). Options none, till now + 1 day (MIT `ticket_lifetime`).
- TGS: `PA-TGS-REQ` authenticator with the session key's keyed checksum of the body, no subkey; till
  the TGT's end time; sent to the realm the TGT is for.
- Transport: `KerberosKdcSender` (internal) tries each located KDC in order and moves on after an
  `IOException`; UDP within `udp_preference_limit`, TCP otherwise; `KRB_ERR_RESPONSE_TOO_BIG` asks the
  same KDC again over TCP; TCP replies over 1 MiB are refused; `https://` KDCs skipped.
- Sensible defaults taken: offered enctypes fixed at 18, 17, 20, 19, 23 (MIT's order of the types
  BL-686 built); 31-bit nonces as MIT sends; `KerberosKdcException.ErrorOf` became a dictionary
  lookup because the 10-arm switch measured cyclomatic complexity 20 with one uncovered branch.
- Tests: `FakeKdc` is an in-memory KDC built from BL-686/BL-687 with fixed keys (`FakeKdcStream` its
  TCP side). 50 new tests; `Measure-CodeQuality.ps1 -Library Curl.Kerberos.UnitLibrary` reports 100%
  line, 100% branch, 0 failing members, worst CRAP 10.
- Follow-ups filed: BL-822 (cross-realm referrals), BL-823 (MS-KKDCP), BL-824 (enctype names from
  `krb5.conf`), BL-825 (store tickets back in the cache).

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
- 2026-09-28: Doing -> Done. KerberosKdcClient gets service tickets from the cache, by TGS with a cached TGT, or from a password by AS with PA-ENC-TIMESTAMP, over UDP/TCP with typed KDC failures
