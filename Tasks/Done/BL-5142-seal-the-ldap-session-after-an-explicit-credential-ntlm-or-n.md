---
id: BL-5142
title: Seal the LDAP session after an explicit-credential NTLM or Negotiate bind
priority: High
assignee: Claude
pipeline: protocol
depends-on: [BL-5141]
touches: [Curl.Original.Protocol.Ldap.UnitLibrary/LdapAuthenticatedBind.cs, Curl.Original.Protocol.Ldap.UnitLibrary/LdapSearch.cs, Curl.Original.Protocol.Ldap.UnitLibrary/LdapExchange.cs, Curl.Original.Protocol.Ldap.UnitLibrary/NtlmLdapLogonAuthentication.cs, Curl.Original.Protocol.Ldap.UnitTests/LdapAuthenticatedNtlmSealTests.cs, Curl.Original.Protocol.Ldap.UnitTests/LdapAuthenticatedNtlmTests.cs, Curl.Original.Protocol.Ldap.UnitTests/LdapAuthSchemeBindTests.cs]
model: opus
requirement: none
created: 2026-11-13
completed: 2026-11-13
---
# BL-5142 — Seal the LDAP session after an explicit-credential NTLM or Negotiate bind

## Goal

An LDAP bind with explicit credentials and `--ntlm` or `--negotiate` offers NTLM NEGOTIATE_SIGN and NEGOTIATE_SEAL and installs the SASL security layer, so every later LDAP PDU is sealed, as the current-user path and curl's WinLDAP already do.

## Context

The remaining part of BL-5094 (finding STD-049, High: no integrity or confidentiality after the bind), split for size; it uses BL-5141's session key exchange and `NtlmSessionSecurity`. Read BL-5094's Context (in `Tasks/Deferred`) for the full repro against a BER mock on 127.0.0.1:24900.

**Expected (curl 8.22.0 Schannel build, WinLDAP).** The Type-1 flags are `b7 82 08 e2` (0xe20882b7: NEGOTIATE_SIGN, NEGOTIATE_SEAL, LM_KEY and KEY_EXCH set, no OEM domain or workstation supplied). After the Type-3 bind every later LDAP PDU is SASL-wrapped: a 4-byte length then a sealed body, never a plain `0x30` LDAPMessage. Against a mock that cannot unseal, curl ends with `curl: (39) LDAP remote: Unavailable`.

**Where to start.** `LdapAuthenticatedBind.cs`: `NegotiateFlags = unchecked((int)0xa208b207)` becomes 0xe20882b7, and `NtlmAsync` answers with BL-5141's key-exchanging answer and calls `exchange.StartSecurityLayer` with a new `ILdapLogonAuthentication` (`NtlmLdapLogonAuthentication.cs`) whose `Wrap`/`Unwrap` use `NtlmSessionSecurity`, as the current-user path does in `WinLdapLogonBind.cs` (about line 188). Check the WinLDAP NTLM wrap layout (signature before or after the sealed data) against `LdapSaslSecurityLayer.cs` and the current-user path's Negotiate wrap. Existing tests `LdapAuthenticatedNtlmTests.cs` and `LdapAuthSchemeBindTests.cs` may pin the old flags.

## Acceptance criteria

- [x] New tests in `Curl.Original.Protocol.Ldap.UnitTests/LdapAuthenticatedNtlmSealTests.cs` prove the Type-1 message flags include 0x10 (SIGN) and 0x20 (SEAL) (`b7 82 08 e2` for the repro's mock), for both `--ntlm` and `--negotiate` with `-u`.
- [x] A test proves that after the final bind response (tag 0x8b, resultCode 0) the next bytes written are a 4-byte length plus a sealed blob, not `0x30 0x84`, using a stand-in that can unseal, and that this stand-in receives the SearchRequest and the unbind.
- [x] A test proves that against a stand-in that cannot unseal, the transfer ends with `CurlExitCode` 39 (`LDAP remote: Unavailable`) as curl does.
- [x] The current-user path stays byte-identical (existing tests green); tests are platform-neutral (no Windows-only API).
- [x] `dotnet build` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Filed by BL-5094's run on lane 4 as the last part of its split; carries BL-5094's acceptance criteria unchanged.
- Added `LdapSearch.cs` to touches: a lost sealed reply after this bind must end `Unavailable`, not the current-user path's `Server Down`, so `Lost` now takes `LdapExchange.WinLdapLostResultCode`; no task in Doing on origin/work/dark-factory names any LDAP file.
- Decision (ADR-1266): seal only when the negotiated flags hold SEAL, extended session security and 128-bit (what `NtlmSessionSecurity` supports); an NTLMv1 challenge (the existing tests' flags 0) stays unsealed. Wrap layout: 16-byte signature then sealed bytes, as the current-user path.
- Tests: `LdapAuthenticatedNtlmSealTests` drives the handler against a stand-in that recovers the exported session key from the Type-3 with the password, unseals the SearchRequest (0x63) and the unbind (0x42) and answers a sealed SearchResultDone; a scripted server that cannot unseal (plain notice of disconnection, close, short buffer, bad signature) ends `(39) LDAP remote: Unavailable`. No Windows-only API; platform-neutral.
- Ldap tests 725 passed; fast tests green; Measure-CodeQuality for Curl.Original.Protocol.Ldap.UnitLibrary: 0 failing members.

## Log

- 2026-11-13: Created.
- 2026-11-13: Backlog -> Doing. Criteria at claim: 21241d7b bbeb9bfe 2eb53dc5 97a37c4d a0b49a29.
- 2026-11-13: Doing -> Done. The -u --ntlm/--negotiate LDAP bind offers SIGN and SEAL and seals every later message; an unreadable sealed reply ends (39) Unavailable
