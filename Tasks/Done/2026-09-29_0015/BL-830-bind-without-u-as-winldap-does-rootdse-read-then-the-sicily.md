---
id: BL-830
title: Bind without -u as WinLDAP does: rootDSE read, then the Sicily NTLM bind
priority: Normal
assignee: Claude
pipeline: protocol
depends-on: [BL-586]
touches: [Curl.Protocol.Ldap.UnitLibrary, Curl.Protocol.Ldap.UnitTests, Documentation/Planning/Decisions/ADR-0166-ldap-is-hand-built-on-iconnection-and-answers-as-each-platforms-winldap-or-openldap-curl-build.md]
requirement: none
created: 2026-09-28
completed: 2026-09-29
---
# BL-830 — Bind without -u as WinLDAP does: rootDSE read, then the Sicily NTLM bind

## Goal

With `LdapDialect.WinLdap` and no `-u`, `LdapProtocolHandler` binds as curl 8.21.0's `ldap_win_bind` does: it reads the rootDSE's `supportedCapabilities` and `supportedSASLMechanisms`, then sends WinLDAP's Sicily NTLM negotiate bind with the logged-on user's credentials (through the BCL's `NegotiateAuthentication`, behind an injected seam so tests need no domain), and fails as WinLDAP fails.

## Context

- ADR-0166, "Bind": the WinLDAP dialect reproduces `ldap_win_bind` without `-u`. BL-586 built the simple bind for both dialects and left the WinLDAP dialect binding anonymously without `-u`.
- Measured in BL-586 (Windows curl 8.21.0, WinLDAP): without `-u` the first message is a SearchRequest, id 1, base `""`, scope base, `neverDerefAliases`, size limit 0, time limit 120 (`02 01 78`), `typesOnly` false, filter `87 0b "objectclass"`, attributes `supportedCapabilities`:
  `30 84 00 00 00 44 02 01 01 63 84 00 00 00 3b 04 00 0a 01 00 0a 01 00 02 01 00 02 01 78 01 01 00 87 0b 6f 62 6a 65 63 74 63 6c 61 73 73 30 84 00 00 00 17 04 15 "supportedCapabilities"`.
  A server that closes then ends with exit 38 `curl: (38) LDAP local: bind via ldap_win_bind Timeout` after about 240 s.
- Measure next with `Record-CurlExchange.ps1 -Script`: answer the rootDSE reads (with and without `supportedSASLMechanisms` naming `GSS-SPNEGO`/`NTLM`), record the Sicily bind messages (`[APPLICATION 0]` with the `sicilyPackageDiscovery`/`sicilyNegotiate`/`sicilyResponse` choices), and each failure.
- `Curl.Authentication.UnitLibrary` holds the NTLM pieces, but a protocol library may reference only Abstractions: the NTLM token source must reach the handler through an interface the composition root supplies.

## Acceptance criteria

- [x] Measured first as above; request bytes, stderr and exit code copied into Notes.
- [x] `Curl.Protocol.Ldap.UnitTests` pin the rootDSE SearchRequest bytes, the Sicily bind messages and each failure's exit code and message through a fake connection and a fake token source.
- [x] The OpenLDAP dialect's anonymous bind is unchanged (its BL-586 tests still pass).
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Protocol.Ldap.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

- Filed by BL-586. If the token-source seam needs a new interface in `Curl.Protocol.Abstractions.UnitLibrary`, add that project to `touches` first.
- Measured 2026-09-28 with `Record-CurlExchange.ps1 -Script`, Windows curl 8.21.0 (WinLDAP), `ldap://127.0.0.1:<port>/dc=example`, no `-u`; 14 recordings (canned rootDSE answers, binds answered 0/14/49, a hand-made NTLM challenge, closes). Requests (WinLDAP five-byte lengths; `id` counts on from 1):
  - rootDSE `supportedCapabilities`: `30 84 00 00 00 44 02 01 <id> 63 84 00 00 00 3b 04 00 0a 01 00 0a 01 00 02 01 00 02 01 78 01 01 00 87 0b "objectclass" 30 84 00 00 00 17 04 15 "supportedCapabilities"`.
  - rootDSE `supportedSASLMechanisms`: `30 84 00 00 00 46 02 01 <id> 63 84 00 00 00 3d ... 30 84 00 00 00 19 04 17 "supportedSASLMechanisms"`.
  - GSS-SPNEGO offered: `30 84 00 00 00 62 02 01 03 60 84 00 00 00 59 02 01 03 04 00 a3 84 00 00 00 4e 04 0a "GSS-SPNEGO" 04 40 <NTLM negotiate, flags b7 b2 08 e2>`; answered 14 with `87 <challenge>`: authenticate message in the same shape, id 4 (its MsvAvTargetName was `ldap/127.0.0.1`).
  - Not offered: `supportedCapabilities` read again (id 3), then Sicily `30 84 00 00 00 54 02 01 04 60 84 00 00 00 4b 02 01 03 04 04 "NTLM" 8a 40 <negotiate>`; answered `success` with the challenge as matchedDN: `... 02 01 03 04 00 8b 82 01 74 <authenticate>` id 5.
  - Either bind then answered `success`: next came `00 00 00 4d 01 00 00 00 <8-byte checksum> 00 00 00 00 <61 sealed bytes>` - the sealed SearchRequest (filed as BL-853).
- Outcomes (stderr, exit): bind answered 49 in both attempts (GSS or Sicily) -> rootDSE re-read, second bind, Unbind, `curl: (38) LDAP local: bind via ldap_win_bind Invalid Credentials`. Close during the first attempt's rootDSE reads -> `curl: (38) LDAP local: bind via ldap_win_bind Timeout` (240-270 s). Close after the GSS bind -> `... Timeout`. Close during the retry's rootDSE reads (after 49, or after a bind WinLDAP gave up on after 30 s) -> `... Server Down`. Close on the retry's bind -> `... Timeout`. Bind answered `success` without a challenge -> WinLDAP started the retry (then `... Timeout` on close). Sealed search unanswered -> `curl: (39) LDAP remote: Server Down`.
- Decisions (ADR-0166 "Measured by BL-830"): the seam is `ILdapLogonTokenSource`/`ILdapLogonAuthentication` in the Ldap library itself (it is the library's own contract, so Abstractions is not touched); `NegotiateLogonTokenSource` wraps the BCL's `NegotiateAuthentication` (package `Negotiate` for GSS-SPNEGO, `NTLM` for Sicily, default credentials, target `ldap/<host>`, EncryptAndSign) and is the default of the two-argument constructor; its tests are Windows-only (`[OSCondition]`) because elsewhere the package needs GSSAPI. Waits reported at once as BL-586 decided. Unmeasured: a premature `success` or a package that yields no token fails the attempt with `Local Error`; `GSS-SPNEGO` matched case-insensitively.
- Code review (code-reviewer): a `success` carrying serverSaslCreds (Kerberos AP-REP, SPNEGO accept-completed) is now given to the package before `IsAuthenticated` is judged; rootDSE values are taken only from the attribute asked for; Sicily closes pinned; the `Negotiate` token test no longer assumes raw NTLM (a domain-joined machine sends SPNEGO). Not pinned: the SPN for an IPv6 literal host (`ldap/` + `IdnHost`, unbracketed), unmeasured.
- Follow-up filed: BL-853 (sign and seal the session after the logon bind). BL-589 now depends on it too, so the handler is not registered before no-`-u` searches work against a real server.
- Added the ADR-0166 file to `touches` to record the measurements; no task in Doing names it (BL-823 touches only ADR-0130).
- Results: `dotnet build Curl.slnx -warnaserror` clean; fast tests green (31 test assemblies; Curl.Protocol.Ldap.UnitTests 467 passed); `Measure-CodeQuality.ps1 -Library Curl.Protocol.Ldap.UnitLibrary`: 100% line, 100% branch, 182 members, 0 failing, worst CRAP 10.

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
- 2026-09-29: Doing -> Done. WinLDAP dialect binds without -u as ldap_win_bind does: rootDSE reads, GSS-SPNEGO or Sicily NTLM bind through ILdapLogonTokenSource, retry and WinLDAP failures
