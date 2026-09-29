---
id: BL-853
title: Sign and seal the WinLDAP session after its logon bind, as WinLDAP does
priority: Normal
assignee: Claude
pipeline: protocol
depends-on: [BL-830]
touches: [Curl.Protocol.Ldap.UnitLibrary, Curl.Protocol.Ldap.UnitTests, Documentation/Planning/Decisions/ADR-0166-ldap-is-hand-built-on-iconnection-and-answers-as-each-platforms-winldap-or-openldap-curl-build.md]
requirement: none
created: 2026-09-28
completed: 2026-09-29
---
# BL-853 — Sign and seal the WinLDAP session after its logon bind, as WinLDAP does

## Goal

After the WinLDAP dialect's logon bind (`GSS-SPNEGO` or Sicily NTLM, BL-830) succeeds, `LdapProtocolHandler` sends and reads every later LDAPMessage wrapped in the SASL security layer with the bind's keys, as WinLDAP does, instead of unsealed.

## Context

- BL-830 measured (Windows curl 8.21.0, WinLDAP, `Record-CurlExchange.ps1 -Script`): after a `GSS-SPNEGO` bind answered `saslBindInProgress` with an NTLM challenge and then `success`, and after a Sicily `sicilyNegotiate`/`sicilyResponse` bind answered `success`, the next thing curl sent was not a plain SearchRequest but `00 00 00 4d` (a four-byte big-endian length) followed by 77 bytes: a 16-byte NTLM signature `01 00 00 00 <8-byte checksum> 00 00 00 00` (version 1, sequence number 0) and 61 sealed bytes - the SearchRequest. WinLDAP asked for signing and sealing: its NTLM negotiate flags are `b7 b2 08 e2`.
- The tokens come through `ILdapLogonTokenSource`/`ILdapLogonAuthentication` (BL-830); `NegotiateLogonTokenSource` wraps the BCL's `NegotiateAuthentication`, whose `Wrap`/`Unwrap` seal and unseal. The seam grows `Wrap`/`Unwrap` members so tests use a fake.
- Measure first: answer the sealed SearchRequest with a sealed reply. This needs a server that holds the NTLM keys, so the recording script's server side must derive them (for instance a C# file-based app with `NegotiateAuthentication` in server mode answering the bind), or the reply bytes must be computed from a known password with a hand-built NTLM server; decide and record which in ADR-0166.
- ADR-0166, "Bind".

## Acceptance criteria

- [x] Measured first; the sealed SearchRequest's framing, WinLDAP's answer to a sealed reply, and to an unsealed one, copied into Notes.
- [x] `Curl.Protocol.Ldap.UnitTests` pin the wrapped search and UnbindRequest bytes after both logon binds through a fake connection and a fake authentication, and the failure a reply that does not unwrap gives.
- [x] The simple bind (`-u`) and the OpenLDAP dialect are unchanged (their tests still pass).
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Protocol.Ldap.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

- Filed by BL-830, which leaves the search after a logon bind unsealed.
- Measuring server (decided, ADR-0166 "Measured by BL-853"): a throwaway C# file-based app outside the repository answering the bind with the BCL's `NegotiateAuthentication` in server mode (package `NTLM`) and sealing/unsealing with its `Wrap`/`Unwrap`, rather than a hand-built NTLM server with a known password - the BCL holds the server side and loopback NTLM needs no password. `Record-CurlExchange.ps1 -Script` plays canned bytes only and cannot compute a sealed reply, and its host, PowerShell 5.1 on .NET Framework, has no `NegotiateAuthentication`, so the recorder was not extended. Like the recorder, it served the second connection WinLDAP opens and speaks on.
- Measured 2026-09-29, Windows curl 8.21.0 (WinLDAP), `-sS ldap://127.0.0.1:<port>/dc=example?cn?base`, no `-u`, seven runs:
  - Framing, both binds (GSS-SPNEGO: challenge in serverSaslCreds, then `success`; Sicily: challenge in matchedDN, then `success`): SearchRequest id 5 (GSS) / id 6 (Sicily) sent as `00 00 00 51 01 00 00 00 <8-byte checksum> 00 00 00 00 <65 sealed bytes>`; the server's `Unwrap` gave `30 84 00 00 00 3b 02 01 05 63 84 ... 30 84 00 00 00 04 04 02 "cn"`, encrypted. The UnbindRequest followed as `00 00 00 1b 01 00 00 00 <checksum> 01 00 00 00 <11 sealed bytes>` = `30 84 00 00 00 05 02 01 06 42 00`. Each request is its own buffer; sequence numbers 0 then 1.
  - Sealed reply (entry and SearchResultDone each in its own buffer, or both in one buffer): stdout `DN: dc=example` / `\tcn: one` / blank, exit 0, sealed UnbindRequest sent.
  - Sealed reply with one checksum byte flipped: WinLDAP reset the connection at once, no UnbindRequest; `curl: (39) LDAP remote: Server Down`, exit 39, after 0.3 s.
  - Unsealed reply (plain entry + SearchResultDone) with the connection held open: WinLDAP read `30 84 00 00` as an 813 MB buffer length and was still waiting after 15 minutes (stopped). Unsealed reply then the server closing: curl closed at once, no UnbindRequest, `curl: (39) LDAP remote: Server Down` after 242 s.
- Decisions (ADR-0166): `ILdapLogonAuthentication` grew `Wrap`/`Unwrap` (`NegotiateLogonTokenSource` maps them to `NegotiateAuthentication.Wrap` with encryption and `Unwrap`, `null` unless `Completed`); new `LdapSaslSecurityLayer : IConnection` frames each write as length + `Wrap`, and unseals each buffer read; `LdapExchange.StartSecurityLayer` switches to it when a logon bind succeeds and keeps that bind's authentication until the transfer ends (the exchange is now `IAsyncDisposable`; a failed attempt's authentication is disposed at once). A buffer that does not unseal, closes part way, or is longer than 16 MiB reads as a close: exit 39 `LDAP remote: Server Down`, no Unbind, at once (BL-586's rule for WinLDAP's waits). The 16 MiB bound is ours - WinLDAP waits for any length - so an unsealed reply does not make the code allocate 813 MB.
- Tests: the four logon-bind success tests now pin the sealed search and Unbind; new tests pin the measured `00 00 00 4d` framing of the 61-byte search, a buffer holding two replies, an empty buffer, a buffer split across reads, the five non-unwrapping replies (bad signature, unsealed, over 16 MiB, closed in the length, closed in the buffer), and that the retry's authentication is kept until the transfer ends (the fake refuses to seal once disposed). `LdapSaslSecurityLayerTests` pin pass-through, short reads and disposal; `NegotiateLogonTokenSourceTests` (Windows only) seal and unseal against an in-process `NegotiateAuthentication` server and refuse an altered signature.
- Results: `dotnet build Curl.slnx -warnaserror` clean; fast tests green (31 test assemblies; Curl.Protocol.Ldap.UnitTests 483 passed); `dotnet format --verify-no-changes` clean; `Measure-CodeQuality.ps1 -Library Curl.Protocol.Ldap.UnitLibrary`: 100% line, 100% branch, 195 members, 0 failing, worst CRAP 10.
- Added the ADR-0166 file to `touches` to record the measurements and decisions; no task in Doing names it (BL-823 touches only ADR-0130).

## Log

- 2026-09-28: Created.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Done. WinLDAP dialect signs and seals the session after its logon bind: search and Unbind go out through the SASL security layer, sealed replies are unsealed, and a reply that does not unwrap fails with 39 Server Down
