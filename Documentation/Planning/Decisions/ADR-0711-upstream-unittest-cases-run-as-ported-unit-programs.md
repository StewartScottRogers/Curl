# ADR-0711 — Upstream's `unittest` cases run as ported unit programs in `Curl.Conformance.UnitProgram.UnitLibrary`

- Status: Accepted
- Date: 2026-10-19
- Task: BL-2840
- Decided by Claude under Stewart's delegation.

## Context

Upstream curl 8.22.0's `tests/data` holds 84 cases that exercise libcurl's or the tool's
*internal* C functions and data structures, not the command line. They name the feature
`unittest`; upstream builds them as `tests/unit/unit<n>` programs, or as `tests/tunit/tool<n>`
programs for the tool's own functions (1394, 1604, 1621, 1622, 1623 and 1720 name
`<tool>tool%TESTNUMBER</tool>`; case 1306's `<tool>` is `unit1305`). The harness skips every
one today, and the gap office has them `excluded | debug-build-only` (1306:
`libcurl-unit-test`). The precedent is `Curl.Conformance.LibTest.UnitLibrary`, C# ports of
`tests/libtest` programs over the public `Libcurl` API only. The unit programs test things
`Libcurl` does not expose, so their ports must reach the libraries that implement them.

## Decision 1 — the mechanism

- A **unit program** (Glossary) is a C# port of one upstream `unit<n>`/`tool<n>` program:
  `IUnitProgram` with `ProgramName` (`unit1302`, `tool1394`) and `RunAsync`, modelled on
  `ILibTest`, `LibTestContext` and `LibTestResult`, returning the program's failure count as
  the C program does.
- The runner recognises a unit case by the `unittest` feature **and** a program name matching
  `unit<digits>` or `tool<digits>` (`<tool>` for 1394, 1604, 1621 to 1623, 1720; `unit1305`
  for 1306; the `<client>` program for the rest). The program name, not the case number, is
  the key.
- `UnitPrograms.Ported` is an explicit list (no reflection scan; AOT). A case whose program is
  registered runs through it and its exit code and standard output are verified like a libtest
  program's. A case with no registered port is skipped with a reason that names the program
  (`Curl has no port of unit1302`, in the pattern of `LibTestNotPorted`); any other screening
  reason still wins where it applies.
- `UpstreamCurlPlatform` reports `unittest` to a case only through that table, never as a
  platform feature. A case that also names `Debug` or `TrackMemory` is run when its program is
  registered: those features describe the C build (debug-only hooks, `curl_dbg_*` allocator
  tracking), and the port needs neither. Features that name real Curl capability (`http`,
  `sftp`, `DoH`, `proxy`, `ftp`, `NTLM`, `netrc`, `sha512-256`, `large-size`) are still
  checked against the platform. `shuffle-dns` and `verbose-strings` are reported to the case
  that needs them (1608, 1652) by its port only.
- A port that needs an `internal` type uses the library's existing `InternalsVisibleTo`
  pattern; no type becomes public only for the harness without a Note saying so.

## Decision 2 — the home of the ports

New projects `Curl.Conformance.UnitProgram.UnitLibrary` (namespace
`Curl.Conformance.UnitProgram`) and `Curl.Conformance.UnitProgram.UnitTests`, in the flat
layout, the `.UnitTests` directly after its library in `Curl.slnx`, held to the four quality
gates and AOT rules like every `*.UnitLibrary`. Unlike the LibTest library it may reference the
libraries it tests (`Curl.Core`, `Curl.Cryptography`, `Curl.Authentication`, `Curl.Ntlm`,
`Curl.Networking`, `Curl.Protocol.*`, `Curl.Cli`, `Curl.Output`, `Curl.Tls`, `Curl.Engine`,
`Libcurl`), because a unit program exists to reach them; it is referenced only by
`Curl.Conformance.UnitLibrary`'s runner wiring. Why not the LibTest library: its `CLAUDE.md`
forbids it any library below `Libcurl`, which is its point. Why not each port beside its
subject: the case runner needs one table. The names match the proposal, so BL-2841 to BL-2849
keep their `touches` and Goals unchanged.

## Decision 3 — the 84 cases

A `no counterpart` reason is lasting only where the case tests a C data structure or allocator
Curl does not have. Curl uses .NET collections, `Task`s and `Channel`s in their place. None of
the reasons below is that a port is hard or that the BCL lacks a primitive. The batch task
owning each row follows the board; a `no counterpart` row is recorded by BL-2849 (or, for the
two noted, by its batch) with no port.

| Case | Row |
| --- | --- |
| 1300 | no counterpart (BL-2849): `Curl_llist`, libcurl's intrusive linked list |
| 1302 | port in BL-2842: base64 encode and decode (`Base64DataEncoding`) |
| 1303 | port in BL-2848: `Curl_timeleft_ms`, the transfer timeout logic over `TimeProvider` |
| 1304 | port in BL-2846: `NetrcFile` |
| 1305 | no counterpart (BL-2849): libcurl's private hash table behind the DNS cache |
| 1306 | no counterpart (BL-2849): same program as 1305 (`unit1305`) |
| 1309 | no counterpart (BL-2849): the splay tree libcurl keys its timer list on |
| 1323 | port in BL-2848: `curlx_tvdiff`, elapsed-time arithmetic (`TimeProvider`/`TimeSpan`) |
| 1330 | no counterpart (BL-2849): `curl_dbg_*` memory tracking (`TrackMemory`), an allocator wrapper |
| 1394 | port in BL-2843: `parse_cert_parameter()` (`Curl.Cli.UnitLibrary` `--cert` parsing) |
| 1395 | port in BL-2844: `Curl_dedotdotify` (`CurlUrlDotSegments`) |
| 1397 | port in BL-2843: `Curl_cert_hostcheck`, certificate host name matching (TLS library) |
| 1399 | port in BL-2848: `Curl_pgrsTime`, transfer progress timers (`Curl.Output`/`Curl.Engine`) |
| 1600 | port in BL-2842: NTLM (`Curl.Ntlm.UnitLibrary`) |
| 1601 | port in BL-2842: MD5 (`Curl.Cryptography.UnitLibrary`) |
| 1602 | no counterpart (BL-2849): libcurl's hash table with integer keys |
| 1603 | no counterpart (BL-2849): libcurl's hash table with string keys |
| 1604 | port in BL-2845: `tool1604`, file name sanitisation (`Curl.Cli.UnitLibrary`) |
| 1605 | port in BL-2848: negative data lengths given to `Libcurl` functions |
| 1606 | port in BL-2848: speedcheck (`Curl.Engine.UnitLibrary` low-speed logic) |
| 1607 | port in BL-2844: `CURLOPT_RESOLVE` parsing (`ResolveOverrides`) |
| 1608 | port in BL-2847: DNS shuffling (`Curl.Networking.UnitLibrary`) |
| 1610 | port in BL-2842: SHA-256 (`Curl.Cryptography.UnitLibrary`) |
| 1611 | port in BL-2842: MD4 (`Md4`) |
| 1612 | port in BL-2842: HMAC (`Curl.Cryptography.UnitLibrary`) |
| 1614 | port in BL-2844: noproxy and CIDR comparisons (`TransferProxySelection`) |
| 1615 | port in BL-2842: SHA-512/256 (`Curl.Cryptography.UnitLibrary`) |
| 1616 | no counterpart (BL-2849): `uint_hash`, libcurl's integer-keyed hash table variant |
| 1620 | port in BL-2844: `url.c` helpers (`Curl.Core`/`Curl.Networking`/`Curl.Cli` URL code) |
| 1621 | port in BL-2845: `tool1621`, stripping credentials from a URL (`Curl.Cli.UnitLibrary`) |
| 1622 | port in BL-2845: `tool1622`, time and size progress text (`Curl.Output.UnitLibrary`) |
| 1623 | port in BL-2845: `tool1623`, the `--limit-rate` size parser (`Curl.Cli.UnitLibrary`) |
| 1625 | port in BL-2846: `Curl_compareheader` (`Curl.Original.Protocol.Http.UnitLibrary`) |
| 1626 | port in BL-2846: `Curl_copy_header_value` (`Curl.Original.Protocol.Http.UnitLibrary`) |
| 1627 | port in BL-2844: `Curl_get_scheme` (`CurlUrlParser`) |
| 1636 | port in BL-2845: `time2str` and `max6out` (`Curl.Output.UnitLibrary`) |
| 1650 | port in BL-2847: DoH parsing (`DohResponseReader`) |
| 1651 | port in BL-2843: x509 parsing (TLS/certificate code) |
| 1652 | port in BL-2847: `infof` (verbose output; `verbose-strings`) |
| 1653 | port in BL-2844: urlapi port number parsing (`CurlUrlParser`) |
| 1654 | port in BL-2846: alt-svc (`AltSvcCache`, injected `TimeProvider`) |
| 1655 | port in BL-2847: `doh_req_encode` (`DohDnsResolver`) |
| 1656 | port in BL-2843: `GTime2str` (ASN.1 time) |
| 1657 | port in BL-2843: `getASN1Element` (ASN.1 reader) |
| 1658 | port in BL-2847: `doh_resp_decode_httpsrr` (`DohResponseReader`) |
| 1660 | port in BL-2846: HSTS (`HstsCache`, injected `TimeProvider`) |
| 1661 | no counterpart (BL-2849): `bufref`, libcurl's borrowed-buffer reference struct |
| 1663 | port in BL-2844: interface option parsing (`Curl.Networking.UnitLibrary`) |
| 1664 | port in BL-2844: `strparse.c` string parsing (`Curl.Core`/`Curl.Cli` parsers) |
| 1666 | port in BL-2843: `encodeOID` (ASN.1) |
| 1667 | port in BL-2843: `ASN1tostr` (ASN.1) |
| 1668 | port in BL-2845: `ftp_213_date` (`Curl.Original.Protocol.Ftp.UnitLibrary`) |
| 1669 | port in BL-2846: alt-svc load of more than 5000 entries (`AltSvcCache`) |
| 1674 | port in BL-2846: HSTS load of more than 10000 entries (`HstsCache`) |
| 1675 | port in BL-2844: URL API (`CurlUrlParser`) |
| 1676 | port in BL-2843: x509 DH public key parameter extraction |
| 1720 | port in BL-2845: `tool1720`, `create_dir_hierarchy()` (`Curl.Cli.UnitLibrary`) |
| 1961 | port in BL-2844: `curlx_inet_ntop()` and `curlx_inet_pton()` (`Curl.Networking.UnitLibrary`) |
| 1979 | port in BL-2842: AWS SigV4 `canon_string` (`Curl.Authentication.UnitLibrary`) |
| 1980 | port in BL-2842: AWS SigV4 `canon_query` (`Curl.Authentication.UnitLibrary`) |
| 2413 | port in BL-2847: `Curl_peer` (connection peer identity, `Curl.Networking`/`Curl.Engine`) |
| 2600 | no counterpart (BL-2849): connection filter chain connect and destroy; Curl connects through `IConnection`, not filters |
| 2601 | no counterpart (BL-2849): `bufq`, libcurl's chunked buffer queue |
| 2602 | no counterpart (BL-2846 records it): `dynhds`, libcurl's growable array of header structs; the header behaviour is covered by 1625 and 1626 |
| 2603 | no counterpart (BL-2893 records it): `h1_req_parser`, libcurl's incremental parser that rereads its own HTTP/1 request text, fed in chunks, into a request struct (method, scheme, authority, path, header count, unread bytes); Curl builds the HTTP/2 and HTTP/3 header list in one pass from the head it formats (`Http2RequestHeaders`), and the one observable behaviour, a custom method with a space (#19543), is BL-2915 |
| 2604 | port in BL-2845: `Curl_get_pathname` (`Curl.Original.Protocol.Ssh.UnitLibrary`) |
| 2605 | port in BL-2845: `Curl_ssh_range` (`Curl.Original.Protocol.Ssh.UnitLibrary`) |
| 2606 | no counterpart (BL-2849): `Curl_conn_adjust_pollset`, adjusting a C `poll` set; Curl has no pollset |
| 3200 | port in BL-2845: `curl_get_line` (line reader for file-backed stores) |
| 3205 | port in BL-2843: cipher suite name lookup (TLS cipher names) |
| 3211 | no counterpart (BL-2849): `uint_bset`, a C bitset over small integers |
| 3212 | no counterpart (BL-2849): `uint_hashset`, a C hash set of integers |
| 3213 | no counterpart (BL-2849): `uint32_tbl` and `uint_spbset`, C integer table and sparse bitset |
| 3214 | no counterpart (BL-2848 records it): `sizeof` checks of C structs |
| 3216 | port in BL-2848: ratelimit (`Curl.Engine.UnitLibrary`) |
| 3219 | port in BL-2847: `CURLPROTO_MASK` checks (`Curl.Api.UnitLibrary`) |
| 3227 | port in BL-2845: reject CR and LF in a mail MIME part name and file name (`Curl.Engine.UnitLibrary` MIME) |
| 3300 | no counterpart (BL-2849): libcurl's own thread pool; Curl uses the .NET thread pool |
| 3301 | no counterpart (BL-2849): libcurl's thread queue |
| 3302 | port in BL-2847: `CURLOPT_GSSAPI_DELEGATION` reaches the transfer (observable part only) |
| 3303 | port in BL-2847: connection reuse distinguishes mTLS key, cert type, key type, key password (observable part) |
| 3304 | port in BL-2847: TLS session cache peer key uses mTLS key, key type, cert type (`Curl.Tls`/`Curl.Networking`) |
| 3306 | no counterpart (BL-2849): the thread queue's retry of failed thread starts (`CURL_DBG_THRDPOOL_FAIL_STARTS`) |
| 3400 | port in BL-2846: capsule protocol encoder and decoder (`CapsuleDatagramChannel`) |

Counts: 84 rows. 63 are ported. 21 are `no counterpart`: the 18 cases of BL-2849 (1300, 1305,
1306, 1309, 1330, 1602, 1603, 1616, 1661, 2600, 2601, 2606, 3211, 3212, 3213, 3300, 3301, 3306),
plus 2602 (recorded by BL-2846), 2603 (recorded by BL-2893) and 3214 (recorded by BL-2848). A batch task that reads a C program and finds the Curl type absent, or present
where this table says `no counterpart`, changes its own row in its Notes and files the port or
the record as a task; a reason it records is a C-only thing, never "hard".

## Consequences

- BL-2841 builds the mechanism and the empty table; BL-2842 to BL-2848 port; BL-2849 records
  the C-only rows. No project name or `touches` in those tasks changes.
- Gap scorecard rows for ported cases move from `excluded | debug-build-only` to `match`
  once a run measures them; the C-only rows stay excluded with the reason above.
