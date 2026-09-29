---
id: BL-587
title: Read an LDAP URL and send its search request, failing with exit 39
priority: Normal
assignee: Claude
pipeline: protocol
depends-on: [BL-586]
touches: [Curl.Protocol.Ldap.UnitLibrary, Curl.Protocol.Ldap.UnitTests, Record-CurlExchange.ps1, Documentation/Planning/Decisions/ADR-0166-ldap-is-hand-built-on-iconnection-and-answers-as-each-platforms-winldap-or-openldap-curl-build.md]
requirement: none
created: 2026-09-28
completed: 2026-09-28
---
# BL-587 — Read an LDAP URL and send its search request, failing with exit 39

## Goal

`ldap://host/<dn>?<attrs>?<scope>?<filter>?<extensions>` is read as curl 8.21.0 reads it (RFC 4516, percent-decoding, defaults `base` scope and `(objectClass=*)`), the filter string is encoded per RFC 4515, the `SearchRequest` is sent, and a non-success `SearchResultDone` fails with exit 39 (`CURLE_LDAP_SEARCH_FAILED`) and curl's message; a URL curl rejects is refused with its exit code.

## Context

- Conformance audit 2026-09-28, row 37. Builds on BL-586. URL model: BL-585's ADR.
- Measure with `Record-CurlExchange.ps1 -Script`: `ldap://h/dc=example`, `?cn,mail?sub?(uid=a)`, a filter with `&`, `|`, `!`, `*` and an escaped `\2a`, a bad scope, a bad filter, and a search answered `noSuchObject` (32); record `request.bin`, stderr and exit code.

## Acceptance criteria

- [x] Measured first as above; request bytes, stderr and exit code copied into Notes.
- [x] `Curl.Protocol.Ldap.UnitTests` pin the search request bytes for each URL and the outcome for each failure.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Protocol.Ldap.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

### Measured (2026-09-28)

`Record-CurlExchange.ps1 -Script`, `-sS -u cn=u:p`, bind answered `success`, search answered
`success` unless stated. W = curl 8.21.0 WinLDAP (Windows), L = curl 8.18.0 OpenLDAP 2.6.10
(WSL, `-ListenAddress 172.26.96.1`). Bytes are the SearchRequest; every accepted case is
followed by the UnbindRequest with messageID 3 (W `30 84 00 00 00 05 02 01 03 42 00`,
L `30 05 02 01 03 42 00`). Exit 0, empty stderr unless stated.

| URL path | W | L |
| --- | --- | --- |
| `dc=example` | `30 84 00 00 00 37 02 01 02 63 84 00 00 00 2e 04 0a "dc=example" 0a 01 00 0a 01 00 02 01 00 02 01 00 01 01 00 87 0b "ObjectClass" 30 84 00 00 00 00` | `30 2f 02 01 02 63 2a 04 0a "dc=example" 0a 01 00 0a 01 00 02 01 00 02 01 00 01 01 00 87 0b "objectclass" 30 00` |
| `dc=example?cn,mail?sub?(uid=a)` | scope `0a 01 02`, filter `a3 84 00 00 00 08 04 03 "uid" 04 01 "a"`, attributes `30 84 00 00 00 0a 04 02 "cn" 04 04 "mail"` | the same fields, shortest lengths (`30 36 ... a3 08 ... 30 0a ...`) |
| `dc=example??one?(&(\|(uid=a*b)(cn=x\2a))(!(sn=*)))` | `a0 84 00 00 00 35 a1 84 00 00 00 25 a4 84 00 00 00 11 04 03 "uid" 30 84 00 00 00 06 80 01 "a" 82 01 "b" a3 84 00 00 00 08 04 02 "cn" 04 02 "x*" a2 84 00 00 00 04 87 02 "sn"` | `a0 21 a1 19 a4 0d 04 03 "uid" 30 06 80 01 "a" 82 01 "b" a3 08 04 02 "cn" 04 02 "x*" a2 04 87 02 "sn"` |
| `dc=example??bogus` | exit 3 `curl: (3) Bad LDAP URL: Invalid Syntax`, connects, sends nothing | exit 3 `curl: (3) LDAP local: bad or missing scope`, never connects |
| `dc=example???(uid=a` | exit 39 `curl: (39) LDAP remote: Filter Error`; no search, Unbind id 3 | exit 39 `curl: (39) LDAP local: ldap_search_ext Bad search filter`; no search, Unbind id 3 |
| `dc=example`, search answered 32 | exit 39 `curl: (39) LDAP remote: No Such Object`, Unbind | exit 39 `curl: (39) LDAP remote: search failed No such object ` (trailing space), Unbind |

Also recorded, all pinned in `LdapProtocolHandlerTests.Search.cs` and `LdapFilterEncoderTests.cs`:
upper-case scopes, `onetree`/`onelevel`/`subordinate`/`children`, extensions (`!bogusext`,
`????`, `????a,,b`), extra `?x?y` (W ignores, L exit 3 `bad URL`), `%zz`, `%2`, `%00`, `%C3%A9`
and `%80%9f` in each part, attributes `,cn,` and `*,+`, filters without parentheses, two
top-level filters, `>=`/`<=`/`~=`, substrings with `**` and escapes, `\*`, `\zz`, `(cn=)`,
`()`, `(&)`, `(|)`, `(!(a)(b))`, blanks, `(c n=a)`, extensible matches with and without `dn`
and a rule, and trailing garbage. After the search: a diagnostic message (L prints it after
the text, W does not), `sizeLimitExceeded` (success on both), a server that closes (W exit 39
`LDAP remote: Server Down` after 30 s, L exit 56 `LDAP local: search ldap_result Can't contact
LDAP server`), a reply for another messageID (both ignore it), and a reply to the search that
is not a search response (W ignores it; L exits 0 after `30 06 02 01 03 50 01 02` Abandon and
Unbind id 4). libldap's texts for result codes 1 to 128 and 4096 are in `LibLdapResultText`.

### Decisions and defaults

- Everything measured is decided in ADR-0166's new "Measured by BL-587" section (decided by
  Claude under Stewart's delegation), including the few filter corners left unmeasured.
- WinLDAP's 30-second wait before `Server Down` is not reproduced; the outcome is reported at
  once, as BL-586 did for the bind.
- The Windows ANSI code page is taken as Windows-1252, the measuring machine's.
- Touches widened (no task in Doing names either): `Record-CurlExchange.ps1`, whose `-Script`
  mode threw when curl exited without writing on any connection (the Windows build refusing
  a URL after connecting), and ADR-0166, where the measurements are recorded.
- The handler tests for the search live in `LdapProtocolHandlerTests.Search.cs`, a part of
  the one partial `LdapProtocolHandlerTests` class, as `CookieStoreTests.*.cs` does.

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
- 2026-09-28: Doing -> Done. ldap:// URLs are read as WinLDAP and OpenLDAP curl read them, filters encoded per RFC 4515, the SearchRequest sent, and failures end with exits 3, 39 and 56 and each build's message
