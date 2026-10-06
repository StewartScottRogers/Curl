---
id: BL-538
title: Answer SASL NTLM and GSSAPI through the NTLM and Negotiate seam
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-526, BL-527, BL-536, BL-691, BL-851]
touches: [Curl.Authentication.UnitLibrary, Curl.Authentication.UnitTests, Documentation/Planning/Decisions/ADR-0184-sasl-gssapi-and-ntlm-run-on-one-security-context-per-exchange.md, Documentation/Planning/Decisions/README.md]
requirement: none
created: 2026-09-28
completed: 2026-09-29
---
# BL-538 — Answer SASL NTLM and GSSAPI through the NTLM and Negotiate seam

## Goal

The SASL authenticator answers the NTLM and GSSAPI mechanisms with the token source BL-525's ADR chose (built by BL-526 and BL-527), and ranks them as curl 8.21.0 does, so mail servers that offer only those work on every platform.

## Context

- Conformance audit 2026-09-28, rows 23 and 34. GSSAPI uses the service name `smtp`/`imap`/`pop` by default and `--service-name` overrides it (row 23's service-name task wires the option).
- RFC 4752 (GSSAPI SASL, including the security-layer negotiation message after the context completes, which needs GSS Wrap/Unwrap from BL-691). Standing rule (root `CLAUDE.md`, "Decisions", 2026-09-28): both mechanisms are offered on every platform; SASL GSSAPI uses the raw Kerberos mechanism (`Curl.Kerberos.UnitLibrary`), not SPNEGO.

## Acceptance criteria

- [x] Measured first with `Record-CurlExchange.ps1 -Smtp` offering `AUTH NTLM` with a fixed `334` Type 2 challenge; the messages curl sent copied into Notes (GSSAPI cannot be measured without a KDC; say so in Notes).
- [x] `Curl.Authentication.UnitTests` with a fake token source pin the NTLM SASL exchange and the GSSAPI exchange including the final security-layer message, and the ranking.
- [x] Both mechanisms are offered and complete on every platform (no `OSCondition` refusal); where platforms' measured bytes differ, each is pinned in its own `OSCondition` test.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Authentication.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

- 2026-09-29, lane 1: stopped on a contract gap, filed as BL-851 and added to `depends-on`.
  `ISaslExchange` is synchronous, but `ISecurityContext.NextTokenAsync` is asynchronous
  (the hand-built Kerberos first step asks the KDC), and `ISecurityContext` has no GSS
  Wrap/Unwrap for RFC 4752's security-layer message. Both live in
  `Curl.Protocol.Abstractions` and reach the SMTP, IMAP and POP3 handlers and
  `Curl.Console.UnitTests` (which BL-732 holds), so it is its own task rather than a widening
  of this one. Wiring the factory into `CurlComposition` is filed as BL-852 (depends on this).
- Measured with curl 8.21.0 (Schannel, Windows), `Record-CurlExchange.ps1 -Smtp -SmtpReply
  'EHLO=250-localhost\r\n250 AUTH NTLM'`, `-u 'DOMAIN\user:pass'`; the recorder now answers
  `AUTH NTLM` with a bare `334 ` (no initial response) and then the fixed Type 2
  `TlRMTVNTUAACAAAADAAMADgAAAAzgoriASNFZ4mrze8AAAAAAAAAACQAJABEAAAABgBwFwAAAA9TAGUAcgB2AGUAcgACAAwARABvAG0AYQBpAG4AAQAMAFMAZQByAHYAZQByAAAAAAAA`
  (the one `HandBuiltNtlmSecurityContextTests.MeasuredChallenge` pins). Exit 0 both ways.
  - Without `--sasl-ir`: `AUTH NTLM`, `334 `, then Type 1
    `TlRMTVNTUAABAAAAB4IIogAAAAAAAAAAAAAAAAAAAAAKAPRlAAAADw==` (SSPI's, with version),
    `334 <Type 2>`, then a Type 3 (varies per run: client challenge and time), `235`.
  - With `--sasl-ir`: `AUTH NTLM TlRMTVNTUAABAAAAB4IIogAAAAAAAAAAAAAAAAAAAAAKAPRlAAAADw==`,
    `334 <Type 2>`, Type 3, `235`.
  - The SSPI Type 3 carries `MsvAvTargetName` `smtp/127.0.0.1`: the context's service is the
    SASL service name (`smtp`) on the URL's host, not `HTTP`.
- GSSAPI cannot be measured here: it needs a KDC and a keytab for `smtp/<host>`, which the
  loopback recorder does not have. Pin it from RFC 4752 and curl's `lib/vauth/krb5_gssapi.c`
  / `krb5_sspi.c`: initial response is the Kerberos (not SPNEGO) token for
  `<service>@<host>`, then the AP-REP answer is an empty response, then the server's wrapped
  4-byte security-layer offer is answered with a wrapped `01 00 00 00` + authzid (no layer,
  size 0).
- 2026-09-29, lane 3: delivered (ADR-0184). `SaslAuthenticator` takes an optional
  `ISecurityContextFactory` (new `SaslAuthenticator(Encoding, ISecurityContextFactory)`); without
  one GSSAPI and NTLM stay not offered, so `CurlComposition` keeps today's behaviour until BL-852.
  `SecurityContextSaslExchange` holds one context per exchange (SASL keeps one connection, so
  no fresh context per leg as HTTP NTLM needs). GSSAPI steps the context until it is
  established rather than assuming curl's no-mutual path, so an acceptor sending an AP-REP
  first and one sending the offer at once both work; it asks `ProtectionLevel.Sign`.
- Ranking measured 2026-09-29 (curl 8.21.0 Schannel, `-SmtpReply
  'EHLO=250-localhost\r\n250 AUTH GSSAPI NTLM PLAIN','AUTH=334 '`, passed through
  `powershell -Command` so the array survives): `-u u:p` picks NTLM; `-u :` picks GSSAPI.
  `-u DOMAIN\u:p` with only GSSAPI offered picks GSSAPI. Matches `Curl_auth_user_contains_domain`.
- Measured failure path (no KDC): a Kerberos step that fails ends curl with exit 94 and nothing
  more sent (without `--sasl-ir`: `AUTH GSSAPI`, `334 `, close; with it: no `AUTH`). Ours answers
  `null`, so `*` and exit 67, because ADR-0183's contract has no way to carry exit 94. Filed as
  BL-856 (touches Abstractions and the three mail handlers).
- Platform pins: `Begin_NtlmOnSspi_SendsSspisType1AndAType3` (Windows, SSPI's first 16 bytes as
  measured; the OS version block varies) and `Begin_NtlmOnCurlsOwnNtlm_SendsCurlsType1AndAType3`
  (elsewhere, curl's own Type 1). GSSAPI runs end-to-end on the hand-built Kerberos against
  `FakeKdc`/`FakeGssAcceptor`; that KDC only issues `HTTP/server.example.test`, so the test
  names it with `--service-name HTTP` and the `smtp` naming is pinned by the scripted test.
- Added the ADR file and `Documentation/Planning/Decisions/README.md` (index rows for ADR-0183,
  which BL-851 left out, and ADR-0184) to `touches`; no task in Doing names either.

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
- 2026-09-29: Doing -> Backlog. Waits on BL-851: the SASL exchange contract is synchronous and ISecurityContext has no GSS Wrap/Unwrap, both in Curl.Protocol.Abstractions
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Done. SASL GSSAPI and NTLM are ranked as curl 8.21.0 ranks them and answered through the security context factory on every platform
