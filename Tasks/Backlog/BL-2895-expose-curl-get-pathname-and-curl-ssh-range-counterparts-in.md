---
id: BL-2895
title: Expose Curl_get_pathname and Curl_ssh_range counterparts in Curl.Protocol.Ssh to the unit-program ports, for unit2604 and unit2605
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Ssh.UnitLibrary/Sftp/SftpQuoteCommand.cs, Curl.Protocol.Ssh.UnitLibrary/Sftp/SftpDownloadPart.cs, Curl.Protocol.Ssh.UnitLibrary/Curl.Protocol.Ssh.UnitLibrary.csproj, Curl.Protocol.Ssh.UnitTests/Sftp/SshPathnameAndRangeTests.cs]
requirement: none
created: 2026-10-21
completed:
---
# BL-2895 — Expose Curl_get_pathname and Curl_ssh_range counterparts in Curl.Protocol.Ssh to the unit-program ports, for unit2604 and unit2605

## Goal

Curl's counterparts of `Curl_get_pathname` (the SFTP quote word reader, in `SftpQuoteCommand`) and `Curl_ssh_range` (in `SftpDownloadPart`) can be called on their own from the unit-program ports, so the ports of `tests/unit/unit2604.c` and `unit2605.c` (BL-2845) pass.

## Context

unit2604: quoted and unquoted words, the escapes `\"`, `\'` and `\\`, an unknown escape or an unclosed quote is `CURLE_QUOTE_ERROR`, `/~/` expands to the home directory, a word of 90720 bytes is `CURLE_TOO_LARGE`, and the rest after the word. unit2605: start and size for `a-b`, `-n` and `a-`, with `CURLE_RANGE_ERROR` for `-0`, `--2`, a leading or trailing space, `3-2`, `101-102` and `100-` on a 100-byte file. Sources: https://github.com/curl/curl/blob/curl-8_22_0/lib/vssh/vssh.c and the two unit files. Expose each as a function returning curl's code and its outputs.

## Acceptance criteria

- [ ] A `Curl_get_pathname` counterpart gives every row of unit2604.c (code, word, rest), and a `Curl_ssh_range` counterpart every row of unit2605.c (code, start, size).
- [ ] `Curl.Conformance.UnitProgram.UnitLibrary` can call the counterpart (public, or `InternalsVisibleTo Include="Curl.Conformance.UnitProgram.UnitLibrary"` in its library's csproj), so BL-2845 can port the program without touching this library.
- [ ] A focused unit test in the library's own `.UnitTests` project pins every row of the C program's table (ADR-0482).
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and the changed library keeps 100% line and branch coverage and complexity at most 10.

## Notes

## Log

- 2026-10-21: Created (split from BL-2845, which waits on it).
