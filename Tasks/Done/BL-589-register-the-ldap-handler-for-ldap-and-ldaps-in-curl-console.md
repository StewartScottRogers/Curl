---
id: BL-589
title: Register the LDAP handler for ldap and ldaps in Curl.Console with its -v lines
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-588, BL-830, BL-853]
touches: [Curl.Cli.UnitLibrary, Curl.Cli.UnitTests, Curl.Console, Curl.Console.UnitTests, Curl.Protocol.Ldap.UnitLibrary, Curl.Protocol.Ldap.UnitTests]
requirement: none
created: 2026-09-28
completed: 2026-09-29
---
# BL-589 — Register the LDAP handler for ldap and ldaps in Curl.Console with its -v lines

## Goal

`curl ldap://...` and `curl ldaps://...` run end to end through `Curl.Console`, and `-v` writes the lines the platform's curl 8.21.0 build writes for an LDAP transfer.

## Context

- Conformance audit 2026-09-28, row 37. Handler: BL-586 to BL-588.
- Register in `Curl.Console/CurlComposition.cs`; dispatch in `Curl.Core.UnitLibrary/ProtocolDispatcher.cs` must accept `ldap`/`ldaps` with ports 389 and 636; add them to the `-V` protocol list as ADR-0021 requires if that list is built here. Events go through `ITransferEvents` (ADR-0046).
- Measure `-v` for a successful search and a failed bind with `Record-CurlExchange.ps1 -Script`.

## Acceptance criteria

- [x] Measured first as above; stderr copied into Notes with varying parts marked.
- [x] `Curl.Console.UnitTests` run an `ldap://` search and an `ldaps://` variant through fake connectors, pinning stdout, stderr, exit code and the `-v` lines.
- [x] `curl -V` lists `ldap` and `ldaps`, with a test.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for each library changed.

## Notes

- 2026-09-29 (lane 1): The `-V` `Protocols:` line is the constant `CurlVersionText.ProtocolsLine` in `Curl.Cli.UnitLibrary`, pinned by `Curl.Cli.UnitTests/CurlVersionTextTests.cs`, and ADR-0021 Decision 6 requires it to change in the same change that registers the handler. So `touches` now names `Curl.Cli.UnitLibrary` and `Curl.Cli.UnitTests`. BL-645 (in Doing) touches both, so the task went back to Backlog until BL-645 is done. `ProtocolDispatcher` needs no change: it dispatches any scheme a registered handler claims, and `LdapProtocolHandler` already defaults the ports to 389 and 636.

### Measured (2026-09-29, lane 3)

`Record-CurlExchange.ps1 -Script`, `-sv -u cn=u,dc=x:secret ldap://127.0.0.1:18389/dc=example`;
bind answered `success`, then one entry `dc=example` with `ou: x`
(`30 1e 02 01 02 64 19 04 0a "dc=example" 30 0b 30 09 04 02 "ou" 31 03 04 01 "x"`) and a
SearchResultDone `success`. W = curl 8.21.0 WinLDAP (Windows), L = curl 8.18.0 OpenLDAP 2.6.10
(WSL, `-ListenAddress 172.26.96.1`). `<port>` marks the varying local port.

W, search (exit 0, stdout `DN: dc=example\n\tou: x\n\n`):
```
*   Trying 127.0.0.1:18389...
* Established connection to 127.0.0.1 (127.0.0.1 port 18389) from 127.0.0.1 port <port> 
* LDAP local: LDAP Vendor = Microsoft Corporation. ; LDAP Version = 510
* LDAP local: ldap://127.0.0.1:18389/dc=example
* LDAP local: trying to establish cleartext connection
{ [4 bytes data]
* shutting down connection #0
```
W, bind `invalidCredentials` twice (exit 38): the first five lines, then
`* LDAP local: bind via ldap_win_bind Invalid Credentials`, `* shutting down connection #0`.
W, search `noSuchObject` (exit 39): the first five, `* LDAP remote: No Such Object`, `* shutting down connection #0`.
W, `ldap://127.0.0.1:18389/dc=example??bogus` (exit 3): Trying, Established, vendor, URL,
`* Bad LDAP URL: Invalid Syntax`, `* shutting down connection #0`.
W, `ldaps` with `-k`: Trying, `* schannel: disabled automatic use of client certificate`,
`* schannel: using IP address, SNI is not supported by OS.`, Established, vendor, URL,
`* LDAP local: trying to establish encrypted connection`, then WinLDAP's own TLS connection
refuses the recorder's self-signed certificate (`-k` does not reach it): exit 38
`bind via ldap_win_bind Server Down`. A successful `ldaps` search could not be measured.
W, URL line: `LDAP://a:b@127.0.0.1:18389?x??bogus#frag` -> `ldap://a:b@127.0.0.1:18389/?x??bogus#frag`;
`ldap://LocalHost:389/%41?x??bogus` -> as typed.

L, search (exit 0, stdout `DN: dc=example\n\tou: x\n\n\n`):
```
*   Trying 172.26.96.1:18389...
* Established connection to 172.26.96.1 (172.26.96.1 port 18389) from 172.26.99.197 port <port> 
* LDAP local: ldap://172.26.96.1:18389/dc=example
{ [4 bytes data]
* Connection #0 to host 172.26.96.1:18389 left intact
```
L, bind `invalidCredentials` (exit 67): Trying, Established, `* closing connection #0`; no
message line (`Login denied` is curl's text for 67, not a failf).
L, search `noSuchObject` (exit 39): Trying, Established, URL,
`* LDAP remote: search failed No such object `, `* closing connection #0`.
L, `ldap://127.0.0.1:1/dc=x?a?bogus` (exit 3): `* LDAP local: bad or missing scope`,
`* closing connection #-1`, nothing else.

### Decisions and defaults

- The lines live in `LdapVerboseLines`; the connector's own lines come from `ConnectTarget.Events`, which the handler now sets.
- A failure's message is written as a `-v` line except `Login denied`, `LDAP: cannot bind` and `Failure when receiving data from the peer`: those are curl's `curl_easy_strerror` texts, returned without `failf`, as the measured 67 shows.
- The URL line is the typed URL with the scheme in lower case and `/` inserted when the path is empty (measured), and `ldap://` prefixed to a scheme-less URL (not measured; curl's URL API adds the guessed scheme).
- WinLDAP `ldaps` success lines are the `ldap` ones with `encrypted` for `cleartext`, as `lib/ldap.c` words it; not measurable here.
- Each entry piece written to the output is reported as received data, before the write, so `-v` shows `{ [N bytes data]` once (the writer collapses the rest).
- The composition picks the dialect with `OperatingSystem.IsWindows()`; the Console tests pin WinLDAP on Windows and OpenLDAP elsewhere with `OSCondition`.
- ADR-0166 could not be edited: BL-883 in Doing holds `Documentation/Planning/Decisions`. Filed BL-887 to add a "Measured by BL-589" section from these Notes.
- Not examined: the handler does not pass `context.Proxy` to its connect target, as IMAP does; whether curl tunnels LDAP through `-x` was out of this task's scope.

## Log

- 2026-09-28: Created.
- 2026-09-28: Now depends on BL-853 (BL-830): WinLDAP seals the session after its logon bind, so the handler waits for that before it is registered.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Backlog. Needs Curl.Cli.UnitLibrary and Curl.Cli.UnitTests (the -V Protocols line, ADR-0021 Decision 6), which BL-645 in Doing touches; restart once BL-645 is done.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Done. curl ldap:// and ldaps:// run through Curl.Console with each build's measured -v lines, and -V lists ldap and ldaps
