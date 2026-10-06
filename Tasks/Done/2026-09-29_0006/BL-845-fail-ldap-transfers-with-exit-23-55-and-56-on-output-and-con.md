---
id: BL-845
title: Fail LDAP transfers with exit 23, 55 and 56 on output and connection I/O errors as curl does
priority: Normal
assignee: Claude
pipeline: protocol
depends-on: [BL-588]
touches: [Curl.Protocol.Ldap.UnitLibrary, Curl.Protocol.Ldap.UnitTests, Record-CurlExchange.ps1, Documentation/Planning/Decisions/ADR-0166-ldap-is-hand-built-on-iconnection-and-answers-as-each-platforms-winldap-or-openldap-curl-build.md]
requirement: none
created: 2026-09-28
completed: 2026-09-29
---
# BL-845 — Fail LDAP transfers with exit 23, 55 and 56 on output and connection I/O errors as curl does

## Goal

An LDAP transfer whose output stops accepting bytes fails with exit 23 and curl's `Failure writing output to destination, passed N returned M` message, and one whose connection throws an `IOException` on a send or a receive fails with curl's exit code and message for each build, instead of the exception leaving `LdapProtocolHandler`.

## Context

- Found in BL-588: `LdapEntryWriter` writes to `ITransferContext.Output` and `LdapExchange` to the `IConnection`, and neither catches `IOException` or `OutputWriteFailedException`; ADR-0166 "Measured by BL-588" names this task.
- Pattern to follow: `GopherProtocolHandler` and `GopherTransferMessages.OutputWriteFailed` (exit 23 with `OutputWriteFailedException.BytesAccepted`).
- curl writes an LDAP entry in pieces (`lib/ldap.c`, `lib/openldap.c` call `Curl_client_write` per `DN: `, name, value, line end), so `passed N` is the size of one piece; measure with `Record-CurlExchange.ps1 -Script` and `-o` to a full or closed destination on both builds (Windows curl 8.21.0 WinLDAP; Linux OpenLDAP through WSL, `-ListenAddress`).

## Acceptance criteria

- [x] Measured first: stderr and exit code for an output that fails after the first entry, and for a server that resets the connection mid-search, on both builds; copied into Notes.
- [x] `Curl.Protocol.Ldap.UnitTests` pin exit 23 with the measured `passed`/`returned` numbers for each dialect, and the reset's exit code and message for each dialect.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Protocol.Ldap.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

### Measurements, 2026-09-29

`Record-CurlExchange.ps1 -Script`, `-sS -u cn=u:p`. Windows: curl 8.21.0 WinLDAP (`/mingw64/bin/curl`,
run through Git Bash with `>&-`). Linux: curl 8.18.0 OpenLDAP 2.6.10 in WSL Ubuntu, `-ListenAddress 172.26.96.1`.

| Case | WinLDAP stderr | exit | OpenLDAP stderr | exit |
| --- | --- | --- | --- | --- |
| 2 small entries, stdout closed (under 4096 bytes) | `curl: Failed writing body` | 23 | - | - |
| 100 entries (23-byte DN, 37-byte `description` value), stdout closed / `-o /dev/full` | `curl: (23) Failure writing output to destination, passed 37 returned 4` | 23 | `curl: (23) Failure writing output to destination, passed 37 returned 36` (both `-o /dev/full` and `>&-`) | 23 |
| reset after one entry | `curl: (39) LDAP remote: Server Down` (after 30 s), stdout empty | 39 | `curl: (56) LDAP local: search ldap_result Can't contact LDAP server`, the entry written (18 bytes) | 56 |
| reset instead of the BindResponse | `curl: (38) LDAP local: bind via ldap_win_bind Timeout` | 38 | `curl: (7) LDAP local: connecting ldap_result Can't contact LDAP server` | 7 |
| reset right after the BindResponse (search sent into it) | `curl: (39) LDAP remote: Server Down` | 39 | `curl: (56) LDAP local: search ldap_result Can't contact LDAP server` | 56 |
| close instead of the BindResponse (for comparison) | same as the reset | 38 | same as the reset | 7 |

After the write failure WinLDAP sent its UnbindRequest (messageID 3); OpenLDAP sent an AbandonRequest
(messageID 3, for 2) and then its UnbindRequest (messageID 4).

`passed 37 returned 4` and `passed 37 returned 36` are exactly what curl's per-piece writes predict
(`lib/ldap.c` at curl-8_21_0 and `lib/openldap.c` at curl-8_18_0 call `Curl_client_write` for `DN: `,
the DN, `\n`, `\t`, the name, `:`, the separator, the value, `\n`, then the blank lines): 50 WinLDAP
entries of 81 bytes fill 4050 bytes, the 51st's pieces reach 4092 and its 37-byte value overflows the
4096-byte stdio buffer with 4 bytes of room; 49 OpenLDAP entries of 82 bytes fill 4018, the 50th's reach
4060, room 36.

### Decisions (ADR-0166, "Measured by BL-845")

- No exit 55 and no new exit 56: a reset answers exactly as the server closing does on both builds,
  so an `IOException` on a connection read is read as a close (`LdapMessageReader`), and one on a
  send is swallowed and makes every later read find the server closed (`LdapExchange`). The title's
  "55" is therefore not used; the measured codes are pinned instead.
- `LdapEntryFormatter.FormatPieces` gives curl's pieces, and `LdapEntryWriter` writes them one at a
  time (skipping empty ones, as curl's writer is never called for zero bytes), so `passed` is the
  piece and `returned` comes from `OutputWriteFailedException.BytesAccepted` (0 for any other
  `IOException`), as Gopher does. After the failure OpenLDAP abandons and unbinds, WinLDAP unbinds.
- The under-4096-byte `Failed writing body` case is `Curl.Console`'s (BL-099), not the handler's.

### Scope

- Added `Record-CurlExchange.ps1` to `touches`: its `-Script` mode had no way to reset a connection,
  so it gained a `reset` step (RST via a zero linger time). No task in `Doing` names it.
- Added the ADR-0166 file to `touches` for its "Measured by BL-845" section. No task in `Doing` names it.
- The fast run once failed `Curl.Networking.UnitTests`'
  `AuthenticateAsClientAsync_WithCertStatusAndARevokedStapledResponse_FailsWithExit91AndTheReason (True)`;
  it passed and failed on reruns with no change there, so it is flaky and outside this task.

## Log

- 2026-09-28: Created.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Done. LDAP output failures exit 23 with curl's per-piece passed/returned numbers, and connection resets fail as each build's server-closed answer (38/39 WinLDAP, 7/56 OpenLDAP), as measured
