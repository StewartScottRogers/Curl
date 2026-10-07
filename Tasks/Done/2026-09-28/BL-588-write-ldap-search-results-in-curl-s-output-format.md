---
id: BL-588
title: Write LDAP search results in curl's output format
priority: Normal
assignee: Claude
pipeline: protocol
depends-on: [BL-587]
touches: [Curl.Protocol.Ldap.UnitLibrary, Curl.Protocol.Ldap.UnitTests, Documentation/Planning/Decisions/ADR-0166-ldap-is-hand-built-on-iconnection-and-answers-as-each-platforms-winldap-or-openldap-curl-build.md]
requirement: none
created: 2026-09-28
completed: 2026-09-28
---
# BL-588 — Write LDAP search results in curl's output format

## Goal

Each `SearchResultEntry` is written exactly as the platform's curl 8.21.0 build writes it (the `DN:` line, one tab-indented `attr: value` line per value, base64 for binary values, the blank line between entries), and referrals and `SearchResultDone` are handled as curl handles them.

## Context

- Conformance audit 2026-09-28, row 37. Builds on BL-587. Output format and platform split: BL-585's ADR.
- Measure with `Record-CurlExchange.ps1 -Script`: two entries, a multi-valued attribute, a binary value (bytes outside printable ASCII), an entry with no attributes, and a referral; record stdout bytes exactly.

## Acceptance criteria

- [x] Measured first as above; stdout bytes copied into Notes.
- [x] `Curl.Protocol.Ldap.UnitTests` pin the output bytes for each case; any platform difference the ADR records is pinned with `OSCondition`.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Protocol.Ldap.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

### Measured (2026-09-28)

`Record-CurlExchange.ps1 -Script`, `-sS -u cn=u:p -w [%{size_download}]`, bind answered
`success`, then the entries below and a SearchResultDone `success` unless stated.
W = curl 8.21.0 WinLDAP (Windows), L = curl 8.18.0 OpenLDAP 2.6.10 (WSL,
`-ListenAddress 172.26.96.1`). Stdout as C# text, one character per byte; exit 0, empty
stderr and `size_download` = stdout length unless stated. Every recording is pinned, with its
reply bytes, in `LdapProtocolHandlerTests.Output.cs`.

| Case | W stdout | L stdout |
| --- | --- | --- |
| two entries, `cn`=`a`,`b` and `mail`; `sn` | `DN: cn=a,dc=example\n\tcn: a\n\tcn: b\n\n\tmail: x@y\n\nDN: cn=b,dc=example\n\tsn: z\n\n` | the same with one more `\n` after each entry |
| entry with no attributes, then one with `sn` | `DN: cn=c\nDN: cn=d\n\tsn: z\n\n` | `DN: cn=c\n\nDN: cn=d\n\tsn: z\n\n\n` |
| `00 ff`, ` a`, `a `, `a\tb`, `\ta`, `c3 a9`, `a e9`, `a 01 b`, `a 7f`, `a\nb`, `a\rb`, `~!:<` | `bin:: AP8=`, `lead:: IGE=`, `trail:: YSA=`, `tab: a\tb`, `ltab:: CWE=`, `utf8:: w6k=`, `hi:: Yek=`, `ctl:: YQFi`, `del:: YX8=`, `nl: a\nb`, `cr: a\rb`, `sym: ~!:<` | the same |
| `\na`, `a\n`, `\ra`, `a\vb`, `a\fb`, `a\0b`, ` `, `\t`, `a b` | written as they are but `nul:: YQBi`, `sp:: IA==`, `tabonly:: CQ==` | the same |
| `x;binary`, `y;BINARY`, `zbinary`, `;binary` = `abc`; `mixed` = `ok`, `00 01`, `ok2` | `x;binary:: YWJj`, `y;BINARY:: YWJj`, `zbinary: abc`, `;binary: abc`, `mixed: ok\n\tmixed:: AAE=\n\tmixed: ok2` | the same |
| empty value; `x;binary` empty | `\te: \n`, `\tx;binary:: \n` | `\te:\n`, `\tx;binary::\n` |
| attribute with no values, then `f` = `x` | `DN: cn=n\n\n\tf: x\n\n` | `DN: cn=n\n\tnv:\n\tf: x\n\n\n` |
| DN empty, `cn=c3 a9`, ` cn=sp` | `DN: \n`, `DN: cn=é\n`, `DN:  cn=sp\n` | `DN:\n`, `DN: cn=Ã©\n`, `DN:  cn=sp\n` |
| DN `cn=c4 80`, name `c3 a9`; DN `cn=ff` | `DN: cn=?`, `\té: v`; `DN: cn=?` | the bytes as sent |
| DN `a f0 9f 98 80 b`, `a ff fe b`, `a c2 b e0 a0 b`; names `a e2 82 ac b`, `c2 81 c2 80` (W only) | `DN: a??b`, `DN: a??b`, `\ta\u0080b: 2`, `DN: a?b?b\n\n` (the `c2 81 c2 80` attribute has no values) | - |
| entry, SearchResultReference, entry | both entries | the first entry only |
| entry, then `noSuchObject` | nothing; exit 39 `LDAP remote: No Such Object` | the entry; exit 39 `LDAP remote: search failed No such object `, `size_download` 17 |
| entry, then `sizeLimitExceeded` | the entry | the entry |
| 100-byte value, 60-byte binary | one line each, base64 unwrapped | the same |
| entry with no attribute list (`64 05 04 03 "n=x"`) | `DN: n=x\n` | nothing; AbandonRequest id 3, no Unbind; exit 56 `Failure when receiving data from the peer` |

### Decisions and defaults

- Recorded in ADR-0166's new "Measured by BL-588" section (decided by Claude under Stewart's
  delegation), including the unmeasured corners: an unreadable attribute counts as a missing
  list, an entry without a readable DN is a lost server.
- `touches` widened to ADR-0166, where the measurements are recorded; no task in Doing names it.
- The Windows build's held entries are written in one write once the search succeeds; curl
  writes them piece by piece, which changes nothing a script sees.
- Output-write and connection I/O failures are not handled by the LDAP handler (they were not
  before either); filed as BL-845.

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
- 2026-09-28: Doing -> Done. LDAP search entries are written as WinLDAP and OpenLDAP curl write them: DN and value lines, base64 for binary and unprintable values, each build's blank lines, ANSI names on Windows, referrals and failed searches as each build handles them
