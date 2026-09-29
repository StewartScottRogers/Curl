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
  | No search result in time | 39, `LDAP remote: Server Down` | measured by BL-587 |
  | URL curl refuses | measured by BL-587 | measured by BL-587 |
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
  120) before its NTLM bind. That bind is its own task, BL-825; until it lands the WinLDAP
  dialect binds anonymously without `-u`, and BL-589 registers the handler only after it.

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
