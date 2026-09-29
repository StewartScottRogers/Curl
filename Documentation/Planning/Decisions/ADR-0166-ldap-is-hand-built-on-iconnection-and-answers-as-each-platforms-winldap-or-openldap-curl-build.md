# ADR-0166 — LDAP is hand-built on IConnection and answers as each platform's WinLDAP or OpenLDAP curl build

- **Status:** Accepted
- **Date:** 2026-09-28

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-585.

## Context

`curl` speaks `ldap://` and `ldaps://`; this tool does not yet (conformance audit
2026-09-28, row 37: exits 38 and 39 are never produced). `Curl.Protocol.Ldap.UnitLibrary`
may reference only `Curl.Protocol.Abstractions.UnitLibrary` and must speak through
`IConnection`. `System.DirectoryServices.Protocols` is a NuGet package, not part of the
base class library, and wraps the platform's LDAP library rather than a stream, so it is
out on both counts. `System.Formats.Asn1` is in the BCL.

curl has two LDAP back ends, and the platforms' reference builds use different ones:
`lib/ldap.c` over WinLDAP on Windows, and `lib/openldap.c` over OpenLDAP's `libldap`
elsewhere. They differ on the wire, in their messages, in exit codes and in output.

### Measured

Every case below ran `Record-CurlExchange.ps1 -Script` (BL-532) with canned BER replies,
`-sS -u cn=u:p` against `ldap://<host>:<port>/dc=example` (`--basic` too on Windows).
Windows: curl 8.21.0 (x86_64-w64-mingw32) libcurl/8.21.0 Schannel ... WinLDAP, and the
system32 curl 8.21.0 names WinLDAP as well; both list `ldap ldaps` under Protocols.
Linux: the Ubuntu build under WSL, curl 8.18.0 libcurl/8.18.0 OpenSSL/3.5.5 ...
OpenLDAP/2.6.10, measured through `-ListenAddress` on the WSL network (BL-474); it is the
OpenLDAP build at hand, and the tasks below re-measure on 8.21.0 where one is.

| Case | Windows (WinLDAP) | Linux (OpenLDAP) |
| --- | --- | --- |
| Connections | Two: curl's own stays silent, WinLDAP opens and speaks on a second | One |
| BindRequest (`-u cn=u:p`) | `30 84 00 00 00 15 02 01 01 60 84 00 00 00 0c 02 01 03 04 04 "cn=u" 80 01 "p"` | `30 11 02 01 01 60 0c 02 01 03 04 04 "cn=u" 80 01 "p"` |
| SearchRequest for `dc=example` | id 2, base scope, `neverDerefAliases`, limits 0, `typesOnly` false, filter `87 0b "ObjectClass"`, attributes `30 84 00 00 00 00` | the same fields, filter `87 0b "objectclass"`, attributes `30 00` |
| Success: one entry, `cn` = `a`,`b`; `bin` = `00 ff`; `x;binary` = `00 ff` | exit 0, stdout `DN: dc=example\n\tcn: a\n\tcn: b\n\n\tbin:: AP8=\n\n\tx;binary:: AP8=\n\n` | exit 0, the same bytes plus one more `\n` after the entry |
| End of transfer | UnbindRequest `30 84 00 00 00 05 02 01 03 42 00` | UnbindRequest `30 05 02 01 03 42 00` |
| Bind answered 49 `invalidCredentials` | retries the bind as LDAPv2 (id 2, `02 01 02`); when that too is answered 49: exit 38, `curl: (38) LDAP local: bind via ldap_win_bind Invalid Credentials`; when the server closes instead: exit 38, `... ldap_win_bind Unavailable` | no retry, Unbind, exit 67, `curl: (67) Login denied` |
| Bind answered 53 `unwillingToPerform`, then close | LDAPv2 retry, exit 38, `curl: (38) LDAP local: bind via ldap_win_bind Unavailable` | no retry, Unbind, exit 38, `curl: (38) LDAP: cannot bind` |
| Search answered 32 `noSuchObject` | exit 39, `curl: (39) LDAP remote: No Such Object`, then Unbind | exit 39, `curl: (39) LDAP remote: search failed No such object ` (trailing space: an empty diagnostic message), then Unbind |

BL-532 recorded two more Windows facts: without `-u`, curl binds through `ldap_win_bind`
with the logged-on user's credentials, so WinLDAP first reads the rootDSE's
`supportedCapabilities` and `supportedSASLMechanisms` and then sends a Sicily NTLM
negotiate bind; and a search result that never comes ends after WinLDAP's 30-second wait
with exit 39, `LDAP remote: Server Down`.

## Decision

- **Hand-built on `IConnection`, one library.** `Curl.Protocol.Ldap.UnitLibrary` holds the
  BER codec, the LDAP messages curl uses (BindRequest/Response, SearchRequest,
  SearchResultEntry, SearchResultReference, SearchResultDone, UnbindRequest), the URL
  reader, the filter encoder and the output writer. `LdapProtocolHandler` connects through
  `IConnector`; `ldaps://` asks for TLS from the first byte, as `ConnectTarget` already
  describes. It opens one connection on both platforms: WinLDAP's silent second
  connection is an artefact of WinLDAP, not something a server can tell apart.
- **Read BER with `System.Formats.Asn1`, write with a small hand-built writer.**
  `AsnReader` in `AsnEncodingRules.BER` reads the replies, including long-form lengths,
  as ADR-0163 does for Kerberos; its `AsnContentException` never leaves the library, a
  malformed reply is a typed failure. `AsnWriter` cannot write what WinLDAP writes (every
  constructed element with a five-byte `84 00 00 00 nn` length, primitives with the
  shortest), so requests are written by a hand-built `LdapBerWriter` whose constructed
  length form is a parameter: long form for WinLDAP, shortest for OpenLDAP. A message is
  read whole before it is decoded: tag, definite length, then content, however the bytes
  are split across reads.
- **Match each platform's build, chosen by a dialect.** An `LdapDialect`
  (`WinLdap`, `OpenLdap`) is passed to the handler by `CurlComposition`
  (`OperatingSystem.IsWindows()`), as `PlatformTlsBackend` and `CredentialEncoding` are
  chosen, so tests pin both dialects on every operating system. The dialect decides the
  length form, the default filter's spelling, the LDAPv2 bind retry, the error texts, the
  exit mapping and the extra line after an entry, per the table above.
- **Bind.** With `-u`, a simple bind with the user as the DN and the password, LDAPv3, on
  both. Without `-u`: the OpenLDAP dialect binds anonymously (empty name and password);
  the WinLDAP dialect reproduces `ldap_win_bind` - the rootDSE read, then the Sicily NTLM
  bind as the logged-on user through the BCL's `NegotiateAuthentication` with default
  credentials - measured and pinned by BL-586. A complete reimplementation leaves neither
  out.
- **The URL** follows RFC 4516, `ldap://host[:port]/<dn>?<attributes>?<scope>?<filter>?<extensions>`,
  percent-decoded per part, scope `base` by default, the filter `(objectClass=*)` by
  default (spelled `ObjectClass` by WinLDAP and `objectclass` by OpenLDAP on the wire, as
  measured), and the filter encoded per RFC 4515. Windows' curl reads the URL with its
  own parser in `ldap.c`, OpenLDAP builds with `ldap_url_parse`; one reader here, with
  whatever BL-587 measures differently (a bad scope, a bad filter, a critical extension)
  decided by the dialect.
- **Output** is written as each build writes it: `DN: <dn>\n`, then per attribute one
  `\t<name>: <value>\n` line per value, or `\t<name>:: <base64>\n` when the name ends
  `;binary` or the value is not printable text, then `\n` after each attribute; the
  OpenLDAP dialect writes one more `\n` after each entry. BL-588 measures two entries,
  an entry with no attributes, a referral, and where exactly a value stops counting as
  printable (leading or trailing blanks, bytes above 0x7F), and pins them.
- **Exit codes.**

  | Outcome | WinLDAP | OpenLDAP |
  | --- | --- | --- |
  | Bind not `success` | 38 `LdapCannotBind`, `LDAP local: bind via ldap_win_bind <WinLDAP text>`, after one LDAPv2 retry | 67 `LoginDenied` for 49 `invalidCredentials`, else 38 with curl's own `LDAP: cannot bind` |
  | Connection closed before the bind is answered | 38, `Timeout` when the first bind is unanswered (after a 30-second wait), `Unavailable` when the LDAPv2 retry is (BL-586) | Unbind not sent, exit 7, `LDAP local: connecting ldap_result Can't contact LDAP server` (BL-586) |
  | SearchResultDone not `success` | 39 `LdapSearchFailed`, `LDAP remote: <WinLDAP text>` | 39, `LDAP remote: search failed <libldap text> <diagnostic message>` |
  | No search result in time | 39, `LDAP remote: Server Down` | 56 `RecvError`, `LDAP local: search ldap_result Can't contact LDAP server` (BL-587) |
  | URL curl refuses | 3, `Bad LDAP URL: Invalid Syntax` or `... No Memory`, after connecting (BL-587) | 3, `LDAP local: <ldap_url_parse's reason>`, before connecting (BL-587) |
  | Filter the build refuses | 39, `LDAP remote: Filter Error`, then Unbind (BL-587) | 39, `LDAP local: ldap_search_ext Bad search filter`, then Unbind (BL-587) |
  | Connect and TLS failures | 7, 28, 35, 60 and the rest, from `IConnector` as for every protocol | the same |

  The result-code texts are two tables in the library, one per dialect (WinLDAP's
  `ldap_err2string`, `No Such Object`, `Invalid Credentials`; libldap's, `No such object`,
  `Invalid credentials`), filled in by BL-586 and BL-587 from recordings of each result
  code they use. Timeouts are ADR-0117's: the connector owns `--connect-timeout`, the
  runner owns `--max-time`.

## Measured by BL-586

Decided by Claude under Stewart's delegation, in BL-586, from `Record-CurlExchange.ps1 -Script`
recordings on 2026-09-28 (Windows curl 8.21.0 with WinLDAP; Linux curl 8.18.0 with OpenLDAP
2.6.10 through WSL):

- Both builds number messages from 1 and send the UnbindRequest with the next free
  messageID: 2 after one bind, 3 after WinLDAP's LDAPv2 retry.
- WinLDAP retries every failed result as LDAPv2 (49, 53 and 100 measured) and, when the
  retry also fails, unbinds and reports `ldap_err2string` of the retry's code: `Invalid
  Credentials`, `Unwilling To Perform`, and the empty string for a code it has no text for
  (100). A retry answered `success` goes on as a bound session. The texts are
  `wldap32.dll`'s `ldap_err2stringW` for 0 to 130, read on Windows 11.
- WinLDAP ignores a reply that is not a BindResponse and ends with `Timeout` after its
  30-second wait; so does a first bind the server closes on. The library reports both at
  once rather than waiting: the exit code and message are the same, and a script cannot
  depend on the wait.
- OpenLDAP sends the UnbindRequest after any bind reply but `success`, a reply that is
  not a BindResponse included (exit 38 `LDAP: cannot bind`), and after an anonymous bind
  answered 49 as after a named one (exit 67 `Login denied`).
- Bytes that cannot start an LDAPMessage (not a SEQUENCE, an indefinite or over-long
  length) are treated as a reply that is not a BindResponse; not measured separately.
- Without `-u` the Windows build reads the rootDSE (`supportedCapabilities`, time limit
  120) before its NTLM bind. That bind is its own task, BL-830; until it lands the WinLDAP
  dialect binds anonymously without `-u`, and BL-589 registers the handler only after it.

## Measured by BL-587

Decided by Claude under Stewart's delegation, in BL-587, from about 60 `Record-CurlExchange.ps1
-Script` recordings on 2026-09-28 (the same two builds as BL-586), each with `-sS -u cn=u:p`,
and from curl 8.21.0's `lib/ldap.c` and `lib/openldap.c`:

- **Messages.** Both builds send the SearchRequest with messageID 2 after one bind and the
  UnbindRequest with 3. A filter the build refuses sends no SearchRequest but still uses up
  messageID 2, so the UnbindRequest is 3 there too. `sizeLimitExceeded` (4) ends the search
  successfully in both (`ldap.c` and `openldap.c` both let it through).
- **Reading the URL.** The Windows build reads it with curl's own `ldap_url_parse2_low` once
  connected (its connection opens, nothing is sent, and it fails with exit 3): each part is
  decoded by `Curl_urldecode`, which leaves an invalid `%` as it is and refuses a decoded
  zero byte (`Bad LDAP URL: No Memory`); the scope is not decoded and is one of `base`,
  `one`, `onetree`, `sub`, `subtree`; attributes stop at the first empty one; extensions and
  every part after them are ignored, even a critical `!` one, but a trailing `?` with
  nothing after it is `Invalid Syntax`. The OpenLDAP build reads it with `ldap_url_parse`
  before it connects: each part decoded whole, an invalid `%` empties the part (a DN sent
  empty, a filter refused), a zero byte ends it; scopes add `onelevel`, `subord`,
  `subordinate` and `children` (scope 3) and drop `onetree`; a fifth `?` is `bad URL`, an
  empty extensions part `bad or missing extensions`, and user information in the URL `bad URL`.
- **Text on the wire.** The OpenLDAP build sends the URL's bytes. The Windows build hands them
  to WinLDAP's ANSI functions, which read the system ANSI code page and send UTF-8:
  `%C3%A9` goes out as `c3 83 c2 a9` and `%80` as `e2 82 ac`. The library uses Windows-1252,
  the measuring machine's code page; a filter's `\XX` escape is sent as the raw byte.
- **Filters.** `LdapFilterEncoder` records where the two parsers part: WinLDAP encodes every
  top-level item of `(a=1)(b=2)` and every item under `!`, refuses `(cn=)`, `(&)` and `(|)`,
  keeps a `\` that is not a hex escape, drops empty substrings, sends the text between the
  first `:` and `:=` as the matching rule (`dn:1.2.3`) and always sends `dnAttributes`;
  `libldap` refuses a second top-level item, `!` with two, `\zz`, `(cn=a**b)` and attribute
  names outside `[A-Za-z0-9;.-]`, accepts `(cn=)`, `(&)` and `(|)`, and sends `dnAttributes`
  only when true. Not measured, decided by the same rules: WinLDAP's `(cn=**)` sends an
  empty substrings list, and a `*` in a `>=`, `<=` or `~=` value is literal to WinLDAP and
  refused by `libldap`.
- **Failures after the SearchRequest.** WinLDAP: `LDAP remote: <ldap_err2string>` (the
  BL-586 table), and a server that closes, or answers with anything but a SearchResultDone
  for the search, ends after its 30-second wait as `LDAP remote: Server Down`, reported here
  at once. OpenLDAP: `LDAP remote: search failed <libldap text> <diagnosticMessage>`, the
  texts recorded for every code from 1 to 128 and 4096 (`LibLdapResultText`); a server that
  closes is exit 56; and a reply to the search that is neither an entry nor the
  SearchResultDone ends the transfer successfully, as `oldap_recv` returns end-of-transfer for
  any other message type, after an AbandonRequest (messageID 3) and the UnbindRequest (4).
- `Record-CurlExchange.ps1 -Script` no longer throws when curl exits without writing on any
  connection, as the Windows build does for a URL it refuses after connecting.

## Measured by BL-588

Decided by Claude under Stewart's delegation, in BL-588, from 18 `Record-CurlExchange.ps1 -Script`
recordings on 2026-09-28 (the same two builds as BL-586), each with
`-sS -u cn=u:p -w [%{size_download}]`, the search answered with canned entries:

- **Both builds.** `DN:` line, then per attribute one `\t<name>:` line per value and a blank
  line after the attribute. A value goes to base64 after `::` when the name is longer than
  `;binary` and ends with it in any case (`y;BINARY` yes, `;binary` alone no), or when it is
  not printable: its first or last byte is a space or a tab, or a byte is neither `0x20`-`0x7E`
  nor tab, line feed, vertical tab, form feed or carriage return (so `a\nb` and a leading line
  feed are written as they are; `a\0b`, `a\x7f`, `\xc3\xa9` are base64). An empty value is not
  base64. Base64 is one unwrapped line. `size_download` is the bytes written.
- **WinLDAP.** `DN: <dn>`, values `: <value>` or `:: <base64>` even when empty, an attribute
  with no values only its blank line, an entry with no attributes only its DN line. The DN and
  attribute names come through WinLDAP's ANSI functions: UTF-8 to Windows-1252, `?` for each
  UTF-16 unit without a byte (`U+0100` is `?`, an emoji `??`) and for each invalid UTF-8
  subpart. An attribute whose name does not survive that (`U+0080`) is written with no values,
  since curl asks for the values by the ANSI name. Entries are written only when the search
  succeeds (`ldap_search_s` reads the whole result first): a SearchResultDone of `noSuchObject`
  after an entry writes nothing. A SearchResultReference is skipped. An entry whose attribute
  list is missing is written as its DN line.
- **OpenLDAP.** The DN and names as sent; `DN:` and each `:`/`::` followed by a space and the
  text only when there is text (`DN:` for an empty DN, `\te:` for an empty value); an attribute
  with no values is `\t<name>:` with no blank line after it; one more blank line after each
  entry. Entries are written as they arrive, so a failed search still writes the entries before
  it and counts them in `size_download`. A SearchResultReference ends the transfer after the
  entries so far (BL-587's AbandonRequest and UnbindRequest). An entry whose attribute list is
  missing writes nothing, is abandoned with no UnbindRequest, and fails with exit 56 `Failure
  when receiving data from the peer`.
- Not measured, decided by the same rules: an attribute that is not a type and a set of values
  counts as a missing list; an entry whose DN cannot be read is a reply that is not an
  LDAPMessage (WinLDAP `Server Down`, OpenLDAP exit 56 `Can't contact LDAP server`). Two
  attributes of one entry whose names differ only in case are written each with its own values,
  where WinLDAP would look both up by name.
- A failed write to the output, and a connection that fails with an I/O error rather than
  closing, are not handled by the LDAP handler yet; BL-842 does that.

## Consequences

- BL-586: the BER reader and writer, the message types, and the bind with both dialects,
  failing with 38 or 67 as above.
- BL-587: the URL reader, the RFC 4515 filter encoder and the SearchRequest, failing with
  39 as above and refusing the URLs curl refuses.
- BL-588: the output writer for entries, referrals and SearchResultDone in both dialects.
- BL-589: registration in `Curl.Console` for `ldap` (389) and `ldaps` (636), the dialect
  chosen from the platform, the `-v` lines, and `ldap ldaps` in `-V`.
- Tests pin WinLDAP and OpenLDAP bytes on every operating system, because the dialect is
  a constructor argument, not a runtime check.
- Windows' anonymous bind needs the NTLM exchange under the logged-on user's
  credentials; `NegotiateAuthentication` is BCL and does it on Windows, but off Windows the
  WinLDAP dialect is never chosen, so no test needs a domain.
- The Linux measurements are from curl 8.18.0; if 8.21.0's `openldap.c` differs, the
  task that re-measures records the difference in a new ADR.

## Alternatives considered

- **`System.DirectoryServices.Protocols`.** A package, not the BCL (needs Stewart), it
  wraps WinLDAP or libldap and so cannot run on `IConnection` or be tested offline.
- **Write requests with `AsnWriter`.** It always writes the shortest length, so the WinLDAP
  dialect's five-byte lengths could not be produced; a server cannot tell, but
  `request.bin` would not match the reference build byte for byte.
- **Hand-write the reader too.** More code to get wrong for what `AsnReader` already does
  for Kerberos (ADR-0163); only the writer needs a length form it lacks.
- **One behaviour everywhere** (say, OpenLDAP's). Simpler, but on Windows the exit code
  for a wrong password would be 67 where curl says 38, and scripts can tell.
- **Open WinLDAP's second connection too.** It carries nothing a script or a server
  could observe beyond an idle socket; opening it would only cost a connection.
