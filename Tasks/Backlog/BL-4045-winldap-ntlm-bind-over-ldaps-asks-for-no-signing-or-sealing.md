---
id: BL-4045
title: WinLDAP NTLM bind over ldaps:// asks for no signing or sealing (flags 07B208A2) as curl 8.22.0 does
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Ldap.UnitLibrary/ILdapLogonTokenSource.cs, Curl.Protocol.Ldap.UnitLibrary/NegotiateLogonTokenSource.cs, Curl.Protocol.Ldap.UnitLibrary/WinLdapLogonBind.cs, Curl.Protocol.Ldap.UnitLibrary/LdapProtocolHandler.cs, Curl.Protocol.Ldap.UnitTests/Fakes/FakeLogonTokenSource.cs, Curl.Protocol.Ldap.UnitTests/Cgr289Tests.cs]
requirement: none
created: 2026-10-28
completed:
---
# BL-4045 — WinLDAP NTLM bind over ldaps:// asks for no signing or sealing (flags 07B208A2) as curl 8.22.0 does

## Goal

Through Curl, `curl -sS -k ldaps://localhost:PORT/dc=test` on Windows sends the same WinLDAP NTLM bind as curl 8.22.0 (Schannel, WinLDAP): NTLM negotiate flags `07 b2 08 a2` on the wire (no sign, seal, LM key or key exchange), no security layer after the bind, and the same stderr once the server stops answering.

## Context

BL-4044 measured it (2026-10-28) with `Record-CurlExchange.ps1 -Port 21909 -Tls -Script s.txt -CurlArgs '-sS','-k','ldaps://localhost:21909/dc=test'`, where s.txt is: `read ber`, `send \x30\x0c\x02\x01\x01\x65\x07\x0a\x01\x00\x04\x00\x04\x00`, `read ber`, the same send with message ID 2, `read ber`, the same with ID 3, then `read ber` three times. Both builds send the same 3 rootDSE searches (supportedCapabilities, supportedSASLMechanisms, supportedCapabilities) on WinLDAP's second TLS connection, then a sicilyNegotiate bind (message 4). Real curl's NTLM negotiate flags are `07 b2 08 a2`, Curl's `b7 b2 08 e2`: `NegotiateLogonTokenSource` always asks for `ProtectionLevel.EncryptAndSign`, which WinLDAP does only on plaintext `ldap://`. Real curl then exits 38 `curl: (38) LDAP local: bind via ldap_win_bind Unavailable` after the server goes silent and closes (5 s idle); Curl exits 38 with `... Timeout`. Start in `WinLdapLogonBind.BindAsync` and `ILdapLogonTokenSource.Start`: pass whether the session is over TLS and ask for `ProtectionLevel.None` then. Measure the plaintext `ldap://` flags before changing that path, and the error text against a server that closes after the sicilyNegotiate.

## Acceptance criteria

- [ ] Through real curl and Curl, the command above sends the same bytes on the wire and gives the same exit code and stderr; both results are recorded in Notes.
- [ ] A focused unit test in `Curl.Protocol.Ldap.UnitTests` pins the protection level asked for over `ldaps://` and over `ldap://`.
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

## Log

- 2026-10-28: Created.
