---
id: BL-5094
title: Sign and seal the LDAP session after an explicit-credential NTLM or Negotiate bind
priority: High
assignee: Claude
pipeline: protocol
depends-on: []
touches: [Curl.Original.Protocol.Ldap.UnitLibrary/LdapAuthenticatedBind.cs, Curl.Original.Protocol.Ldap.UnitLibrary/LdapExchange.cs, Curl.Original.Protocol.Ldap.UnitTests/LdapAuthenticatedNtlmSealTests.cs]
model: opus
requirement: none
created: 2026-11-13
completed:
---
# BL-5094 — Sign and seal the LDAP session after an explicit-credential NTLM or Negotiate bind

## Goal

An LDAP bind with explicit credentials and `--ntlm` or `--negotiate` offers NTLM NEGOTIATE_SIGN and NEGOTIATE_SEAL and installs the SASL security layer, so every later LDAP PDU is sealed, as the current-user path and curl's WinLDAP already do.

## Context

From the outside fitness review of 2026-11-13 (commit 56cb97ede, `preview` build), finding STD-049 (High, Confirmed; 3 runs, the orchestrator confirmed the flag value and the missing security layer in the source). Weakens security: no integrity or confidentiality after the bind. LDAP Sicily NTLM bind with explicit credentials, Windows.

**Repro.**
1. Run an LDAP BER mock on 127.0.0.1:24900 (PowerShell or a C# file-based app) that: answers RootDSE searches (base `""`) with `supportedLDAPVersion: 3`; answers a sicilyNegotiate bind (tag 0x8a) with resultCode 0 and an NTLM Type-2 challenge in matchedDN (flags `0xe2898235`, target info `MOCK`); answers a sicilyResponse bind (tag 0x8b) with resultCode 0; answers a search with the entry `cn=a,dc=x`. Log every PDU in hex.
2. `curl -sS -m 15 "ldap://127.0.0.1:24900/dc=x" -u admin:pw --ntlm` (and the same with `--negotiate`).

**Expected (curl 8.22.0 Schannel build, WinLDAP).** The Type-1 flags are `b7 82 08 e2` (NEGOTIATE_SIGN and NEGOTIATE_SEAL set). After the Type-3 bind every later LDAP PDU is SASL-wrapped: a 4-byte length then a sealed body, never a plain `0x30` LDAPMessage. Against this mock (which cannot unseal) curl ends with `curl: (39) LDAP remote: Unavailable`. The port's own current-user path (no `-u`) already seals and matches curl byte for byte.

**Actual.** Type-1 flags `07 b2 08 a2` (no SIGN, no SEAL). After Type-3 the port sends a plain `SearchRequest` (`dc=x ... ObjectClass`) and a plain UnbindRequest, prints `DN: cn=a,dc=x`, and exits 0. Against a domain controller that requires LDAP signing, the unsigned search would also be refused while curl works (inferred, not tested against a real DC).

**Where to start.** `Curl.Original.Protocol.Ldap.UnitLibrary/LdapAuthenticatedBind.cs`: `NegotiateFlags = unchecked((int)0xa208b207)` (about line 24) lacks 0x10 and 0x20, and `NtlmAsync` never installs `LdapSaslSecurityLayer` (only the current-user path does, `LdapExchange.cs` about line 35). Existing tests: `Curl.Original.Protocol.Ldap.UnitTests/LdapAuthenticatedNtlmTests.cs`, `LdapAuthSchemeBindTests.cs` (some may pin the old flags and need updating).

## Acceptance criteria

- [ ] New tests in `Curl.Original.Protocol.Ldap.UnitTests/LdapAuthenticatedNtlmSealTests.cs` prove the Type-1 message flags include 0x10 (SIGN) and 0x20 (SEAL) (`b7 82 08 e2` for the repro's mock), for both `--ntlm` and `--negotiate` with `-u`.
- [ ] A test proves that after the final bind response (tag 0x8b, resultCode 0) the next bytes written are a 4-byte length plus a sealed blob, not `0x30 0x84`, using a stand-in that can unseal, and that this stand-in receives the SearchRequest and the unbind.
- [ ] A test proves that against a stand-in that cannot unseal, the transfer ends with `CurlExitCode` 39 (`LDAP remote: Unavailable`) as curl does.
- [ ] The current-user path stays byte-identical (existing tests green); tests are platform-neutral (no Windows-only API).
- [ ] `dotnet build` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Mock servers in the review were hand-rolled scripts; build the stand-in in the test project in C#, and any hand-check server in PowerShell, a C# file-based app or `Record-CurlExchange.ps1`, never Python.
- Review acceptance check, for a hand check against the mock: Type-1 flags include 0x10 and 0x20; after the 0x8b bind the next bytes are a 4-byte length plus a sealed blob; exit 39 like curl. A test server that unseals NTLM receives the SearchRequest.

- 2026-11-13 (lane 4): Split for size before any edit. The seal needs Curl.Ntlm work outside this task's touches: the AUTHENTICATE message carries no EncryptedRandomSessionKey, the answerer drops the key exchange key, and nothing derives NTLM sign and seal keys. BL-5141 adds that to Curl.Ntlm; BL-5142 (depends on BL-5141) does the LDAP part and carries these acceptance criteria unchanged.

## Log

- 2026-11-13: Created.
- 2026-11-13: Backlog -> Doing. Criteria at claim: 21241d7b bbeb9bfe 2eb53dc5 97a37c4d a0b49a29.
- 2026-11-13: Doing -> Deferred. Split into BL-5141, BL-5142
