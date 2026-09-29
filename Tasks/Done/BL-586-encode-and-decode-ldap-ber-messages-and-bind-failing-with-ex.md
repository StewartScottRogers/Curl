---
id: BL-586
title: Encode and decode LDAP BER messages and bind, failing with exit 38
priority: Normal
assignee: Claude
pipeline: protocol
depends-on: [BL-585, BL-532]
touches: [Curl.Protocol.Ldap.UnitLibrary, Curl.Protocol.Ldap.UnitTests, Documentation/Planning/Decisions]
requirement: none
created: 2026-09-28
completed: 2026-09-28
---
# BL-586 — Encode and decode LDAP BER messages and bind, failing with exit 38

## Goal

An `LdapProtocolHandler` connects through `IConnector` (TLS for `ldaps://`), sends the `BindRequest` curl 8.21.0 sends (anonymous, or simple with `-u`), reads the `BindResponse`, and fails a non-success result with exit 38 (`CURLE_LDAP_CANNOT_BIND`) and curl's message; BER messages split across reads are handled.

## Context

- Conformance audit 2026-09-28, row 37. Design: BL-585's ADR (BER approach, platform behaviour). RFC 4511.
- Measure with `Record-CurlExchange.ps1 -Script` (BL-532): anonymous bind, `-u "cn=u,dc=x:secret"`, a bind answered `invalidCredentials` (49), and a server that closes; record `request.bin`, stderr and exit code.

## Acceptance criteria

- [x] Measured first as above; request bytes, stderr and exit code copied into Notes.
- [x] `Curl.Protocol.Ldap.UnitTests` pin the bind request bytes and outcome for each case through a fake connection, and round-trip the BER encoder and decoder for each type used.
- [x] No test needs `TestCategory=Integration`; tests are platform-neutral; any platform difference the ADR records is pinned per platform.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Protocol.Ldap.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

- Measured 2026-09-28 with `Record-CurlExchange.ps1 -Script`, `-sS -u cn=u,dc=x:secret ldap://<host>:<port>/dc=example` (anonymous: no `-u`). Windows: curl 8.21.0 mingw, WinLDAP. Linux: curl 8.18.0, OpenLDAP 2.6.10, through WSL (`-Curl wsl.exe -ListenAddress 172.26.96.1`), the only OpenLDAP build at hand.
  - WinLDAP bind (-u): `30 84 00 00 00 1f 02 01 01 60 84 00 00 00 16 02 01 03 04 09 "cn=u,dc=x" 80 06 "secret"`; LDAPv2 retry the same with id 2 and `02 01 02`; Unbind `30 84 00 00 00 05 02 01 03 42 00` (id 2 after one bind).
  - WinLDAP 49 then 49: Unbind id 3, exit 38 `curl: (38) LDAP local: bind via ldap_win_bind Invalid Credentials`. 53 then 53: `... Unwilling To Perform`. 100 then 100: `... ldap_win_bind ` (empty text). 49 then success: goes on to the SearchRequest with id 3. 49 then close: `... Unavailable`. Close before any answer: `... Timeout` after ~30 s. Reply `30 05 02 01 01 04 00` (not a BindResponse): ignored, `... Timeout` after ~30 s.
  - WinLDAP without -u: SearchRequest for the rootDSE `supportedCapabilities` (id 1, time limit 120) first; server closes: exit 38 `... Timeout` after 240 s. Left to BL-825 (filed).
  - OpenLDAP bind (-u): `30 1b 02 01 01 60 16 02 01 03 04 09 "cn=u,dc=x" 80 06 "secret"`; anonymous `30 0c 02 01 01 60 07 02 01 03 04 00 80 00`; Unbind `30 05 02 01 02 42 00`.
  - OpenLDAP 49 (named or anonymous): Unbind, exit 67 `curl: (67) Login denied`. 53: Unbind, exit 38 `curl: (38) LDAP: cannot bind`. Not a BindResponse: Unbind, exit 38 `LDAP: cannot bind`. Close before the answer: exit 7 `curl: (7) LDAP local: connecting ldap_result Can't contact LDAP server`.
- WinLDAP's texts come from `wldap32.dll` `ldap_err2stringW` for codes 0-130 (P/Invoke from PowerShell), not from memory.
- Decisions (recorded in ADR-0166, "Measured by BL-586"): WinLDAP's 30-second waits are not reproduced, the outcome is reported at once; bytes that cannot start an LDAPMessage count as a reply that is not a BindResponse; the WinLDAP dialect binds anonymously without `-u` until BL-825 builds the rootDSE read and NTLM bind; BL-589 now depends on BL-825 so the handler is not registered before it.
- Split replies: the recorder has no step to delay between sends, so splitting was pinned only in tests (one byte per read, and inside a long-form length).
- Once bound, the handler sends the UnbindRequest and succeeds with nothing written; the search is BL-587, the output BL-588.
- Added `Documentation/Planning/Decisions` to `touches` to record the measured facts in ADR-0166; no task in Doing names it.
- Results: `dotnet build Curl.slnx -warnaserror` clean; fast tests green (Curl.Protocol.Ldap.UnitTests: 124 passed); `Measure-CodeQuality.ps1 -Library Curl.Protocol.Ldap.UnitLibrary`: 100% line, 100% branch, 38 members, 0 failing, worst CRAP 10.

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
- 2026-09-28: Doing -> Done. LDAP handler binds as WinLDAP or OpenLDAP curl does (BER writer, framed reader, BindResponse decoder), failing with exit 38, 67 or 7 as each build does
