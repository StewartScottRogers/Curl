---
id: BL-847
title: Sign and seal the WinLDAP session after its logon bind, as WinLDAP does
priority: Normal
assignee: Claude
pipeline: protocol
depends-on: [BL-830]
touches: [Curl.Protocol.Ldap.UnitLibrary, Curl.Protocol.Ldap.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-847 — Sign and seal the WinLDAP session after its logon bind, as WinLDAP does

## Goal

After the WinLDAP dialect's logon bind (`GSS-SPNEGO` or Sicily NTLM, BL-830) succeeds, `LdapProtocolHandler` sends and reads every later LDAPMessage wrapped in the SASL security layer with the bind's keys, as WinLDAP does, instead of unsealed.

## Context

- BL-830 measured (Windows curl 8.21.0, WinLDAP, `Record-CurlExchange.ps1 -Script`): after a `GSS-SPNEGO` bind answered `saslBindInProgress` with an NTLM challenge and then `success`, and after a Sicily `sicilyNegotiate`/`sicilyResponse` bind answered `success`, the next thing curl sent was not a plain SearchRequest but `00 00 00 4d` (a four-byte big-endian length) followed by 77 bytes: a 16-byte NTLM signature `01 00 00 00 <8-byte checksum> 00 00 00 00` (version 1, sequence number 0) and 61 sealed bytes - the SearchRequest. WinLDAP asked for signing and sealing: its NTLM negotiate flags are `b7 b2 08 e2`.
- The tokens come through `ILdapLogonTokenSource`/`ILdapLogonAuthentication` (BL-830); `NegotiateLogonTokenSource` wraps the BCL's `NegotiateAuthentication`, whose `Wrap`/`Unwrap` seal and unseal. The seam grows `Wrap`/`Unwrap` members so tests use a fake.
- Measure first: answer the sealed SearchRequest with a sealed reply. This needs a server that holds the NTLM keys, so the recording script's server side must derive them (for instance a C# file-based app with `NegotiateAuthentication` in server mode answering the bind), or the reply bytes must be computed from a known password with a hand-built NTLM server; decide and record which in ADR-0166.
- ADR-0166, "Bind".

## Acceptance criteria

- [ ] Measured first; the sealed SearchRequest's framing, WinLDAP's answer to a sealed reply, and to an unsealed one, copied into Notes.
- [ ] `Curl.Protocol.Ldap.UnitTests` pin the wrapped search and UnbindRequest bytes after both logon binds through a fake connection and a fake authentication, and the failure a reply that does not unwrap gives.
- [ ] The simple bind (`-u`) and the OpenLDAP dialect are unchanged (their tests still pass).
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Protocol.Ldap.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

- Filed by BL-830, which leaves the search after a logon bind unsealed.

## Log

- 2026-09-28: Created.
