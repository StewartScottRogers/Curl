---
id: BL-1329
title: Cut an LDAP search's output at --max-filesize with curl's exit 63 'Exceeded the maximum allowed file size' text
priority: Normal
assignee: Claude
pipeline: protocol
depends-on: []
touches: [Curl.Protocol.Ldap.UnitLibrary, Curl.Protocol.Ldap.UnitTests]
requirement: FR-084
created: 2026-10-03
completed:
---
# BL-1329 — Cut an LDAP search's output at --max-filesize with curl's exit 63 'Exceeded the maximum allowed file size' text

## Goal

An `ldap://` / `ldaps://` search honours `ITransferContext.MaxFileSize` as curl 8.21.0's download writer does: the entry pieces are written until the limit, the piece that crosses it is cut to the bytes left, and the transfer fails with exit 63 and `Exceeded the maximum allowed file size (N) with N bytes`, also reported as a `-v` info line; nothing after it is written.

## Context

- Today `Curl.Protocol.Ldap.UnitLibrary/LdapEntryWriter.cs` writes each piece `LdapEntryFormatter.FormatPieces` gives (one write each, the OpenLDAP build as each entry arrives, the Windows build once the search has succeeded) and never reads `context.MaxFileSize`; the remark on `ITransferContext.MaxFileSize` lists `ldap` among the handlers that do not read it.
- curl 8.21.0 (tag `curl-8_21_0`): both `lib/ldap.c` (WinLDAP) and `lib/openldap.c` write every piece with `Curl_client_write(data, CLIENTWRITE_BODY, ...)`, which reaches `cw_download_write` in `lib/sendf.c`. Lines 251-257 cut a write to the bytes left under `data->set.max_filesize` and write the cut part; lines 286-291 then `failf(data, "Exceeded the maximum allowed file size (%" FMT_OFF_T ") with %" FMT_OFF_T " bytes", ...)` and return `CURLE_FILESIZE_EXCEEDED` (exit 63). A body exactly at the limit does not fail; 0 is no limit. Once a write fails, `ldap.c` stops writing the remaining entries.
- `LdapEntryWriter` already ends the writing on an output failure with `WriteFailure` (exit 23); the limit is a second way to end it, with the same "nothing more is written" rule. Count bytes as `BytesWritten` does, across pieces and entries.
- BL-1305 (TFTP) and BL-1296 (SMB) made the same change in their libraries; follow their tests' shape.

## Acceptance criteria

- [ ] A test in `Curl.Protocol.Ldap.UnitTests` runs a search returning one entry whose formatted output is longer than 10 bytes, with `MaxFileSize = 10`, and asserts exit 63 (`CurlExitCode.FilesizeExceeded`), message `Exceeded the maximum allowed file size (10) with 10 bytes`, exactly the first 10 bytes of the unlimited output written, and that message reported as an info line.
- [ ] The test runs for both `LdapDialect` values (the Windows build and the OpenLDAP build), since they write at different times.
- [ ] A test with two entries and a limit inside the second asserts the first entry is written whole.
- [ ] Tests pin that `MaxFileSize` of 0, `null`, and exactly the output's length complete with exit 0 and the whole output.
- [ ] `dotnet build Curl.Protocol.Ldap.UnitTests -warnaserror` is clean; `dotnet test Curl.Protocol.Ldap.UnitTests --filter "TestCategory!=Integration"` passes; `powershell -NoProfile -File Measure-CodeQuality.ps1 -Library Curl.Protocol.Ldap.UnitLibrary` reports no failing member.

## Notes

## Log

- 2026-10-03: Created.
