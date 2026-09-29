---
id: BL-842
title: Fail LDAP transfers with exit 23, 55 and 56 on output and connection I/O errors as curl does
priority: Normal
assignee: Claude
pipeline: protocol
depends-on: [BL-588]
touches: [Curl.Protocol.Ldap.UnitLibrary, Curl.Protocol.Ldap.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-842 — Fail LDAP transfers with exit 23, 55 and 56 on output and connection I/O errors as curl does

## Goal

An LDAP transfer whose output stops accepting bytes fails with exit 23 and curl's `Failure writing output to destination, passed N returned M` message, and one whose connection throws an `IOException` on a send or a receive fails with curl's exit code and message for each build, instead of the exception leaving `LdapProtocolHandler`.

## Context

- Found in BL-588: `LdapEntryWriter` writes to `ITransferContext.Output` and `LdapExchange` to the `IConnection`, and neither catches `IOException` or `OutputWriteFailedException`; ADR-0166 "Measured by BL-588" names this task.
- Pattern to follow: `GopherProtocolHandler` and `GopherTransferMessages.OutputWriteFailed` (exit 23 with `OutputWriteFailedException.BytesAccepted`).
- curl writes an LDAP entry in pieces (`lib/ldap.c`, `lib/openldap.c` call `Curl_client_write` per `DN: `, name, value, line end), so `passed N` is the size of one piece; measure with `Record-CurlExchange.ps1 -Script` and `-o` to a full or closed destination on both builds (Windows curl 8.21.0 WinLDAP; Linux OpenLDAP through WSL, `-ListenAddress`).

## Acceptance criteria

- [ ] Measured first: stderr and exit code for an output that fails after the first entry, and for a server that resets the connection mid-search, on both builds; copied into Notes.
- [ ] `Curl.Protocol.Ldap.UnitTests` pin exit 23 with the measured `passed`/`returned` numbers for each dialect, and the reset's exit code and message for each dialect.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Protocol.Ldap.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

## Log

- 2026-09-28: Created.
