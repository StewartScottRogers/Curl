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
completed: 2026-09-28
---
# BL-691 — Build and verify GSS-API Kerberos context, wrap and MIC tokens

## Goal

`Curl.Kerberos.UnitLibrary` implements the GSS-API Kerberos V5 mechanism for an initiator: the initial context token (AP-REQ with the RFC 4121 authenticator checksum, flags for mutual authentication, delegation and integrity/confidentiality), verifying the acceptor's AP-REP, and the per-message Wrap and MIC tokens (RFC 4121 section 4.2; the RFC 1964 formats for `rc4-hmac` per RFC 4757), behind the token-source seam BL-525's ADR names.

## Context

- Standing rule (root `CLAUDE.md`, "Decisions", 2026-09-28). Builds on BL-690 (service ticket). Consumers: SPNEGO/Negotiate (BL-692, BL-527), SASL `GSSAPI` with its security-layer message (RFC 4752, BL-538), SOCKS5 GSS-API with RFC 1961 message protection (BL-615), FTP `--krb` with RFC 2228 protection (BL-693), and `--delegation` (BL-631: forwarded TGT in the checksum's `KRB_CRED`).
- RFC 2743 (token framing with the mechanism OID `1.2.840.113554.1.2.2`), RFC 4121 (checksum, sequence numbers, subkeys, `acceptor subkey` rules), RFC 4757 section 7 for `rc4-hmac` tokens.
- Tests drive an in-memory acceptor with fixed keys and an injected random source and time.

## Acceptance criteria

- [x] `Curl.Kerberos.UnitTests` complete a mutual-auth context with the fake acceptor for an AES enctype and for `rc4-hmac`, pin the checksum flags for each `--delegation` level, and round-trip Wrap (with and without confidentiality) and MIC tokens in both directions, rejecting a bad sequence number and a tampered token with typed failures.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Kerberos.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

- Built: `KerberosGssContext` (initial token, AP-REP check, Wrap/Unwrap/GetMic/VerifyMic),
  `KerberosGssContextOptions`, `KerberosGssFlags`, `KerberosDelegation`,
  `KerberosGssException`/`KerberosGssError`, `KerberosGssUnwrapped`, `KerberosGssToken`
  (RFC 2743 framing), `KerberosGssMessageProtection` with `Rfc4121GssMessageProtection`
  and `Rc4HmacGssMessageProtection`, and KRB-CRED (`KerberosCredentialMessage`,
  `KerberosEncryptedCredentialPart`, `KerberosCredentialInfo`, `KerberosMessageType.Credential`).
  `KerberosClock` now holds the seconds-plus-microseconds split `KerberosKdcClient` had.
- Seam: `Curl.Kerberos.UnitLibrary` may not reference `Curl.Protocol.Abstractions.UnitLibrary`,
  so `KerberosGssContext.NextToken`/`Wrap`/`Unwrap` mirror ADR-0142's `ISecurityContext`
  shape and BL-527's hand-built adapter wraps it one to one.
- Decisions (ADR-0171): checksum flags are the requested ones plus confidentiality and
  integrity, as MIT adds them (curl's default gives 0x36, 0x37 with delegation);
  `policy` delegates only on `ok-as-delegate`; delegation needs a caller-supplied
  forwarded TGT, else the flag is dropped as MIT does; KRB-CRED in the session key;
  30-bit random initial sequence number; AP-REP's missing sequence number means 0; RFC 4121
  tokens send EC 0 / RRC 0 and accept any RRC; acceptor tokens must carry exactly the next
  sequence number; checks run framing, integrity, padding, then sequence.
- Tests: `FakeGssAcceptor` implements the acceptor side from the RFCs independently of the
  library. No published RFC 4121 or RFC 4757 token vectors exist, so interop is pinned by
  that independent implementation; no MIT/KDC machine was available to record real tokens.
- Quality: 444 tests in `Curl.Kerberos.UnitTests`; `Measure-CodeQuality.ps1 -Library
  Curl.Kerberos.UnitLibrary`: 100% line, 100% branch, 0 failing members, worst CRAP 10.
- Follow-ups filed: BL-831 (forwarded TGT from the KDC), BL-832 (TLS channel bindings).
- The ADR and its README row sit in `Documentation/Planning/Decisions`, as every task's ADR does.

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
- 2026-09-28: Doing -> Done. Hand-built GSS-API Kerberos initiator: AP-REQ with RFC 4121 checksum and delegation, AP-REP check, RFC 4121 and RFC 4757 Wrap/MIC tokens with typed failures
