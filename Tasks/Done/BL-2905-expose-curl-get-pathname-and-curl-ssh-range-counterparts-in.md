---
id: BL-2905
title: Expose Curl_get_pathname and Curl_ssh_range counterparts in Curl.Protocol.Ssh to the unit-program ports, for unit2604 and unit2605
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Ssh.UnitLibrary/Sftp/SftpQuoteCommand.cs, Curl.Protocol.Ssh.UnitLibrary/Sftp/SftpDownloadPart.cs, Curl.Protocol.Ssh.UnitLibrary/Curl.Protocol.Ssh.UnitLibrary.csproj, Curl.Protocol.Ssh.UnitTests/Sftp/SshPathnameAndRangeTests.cs]
requirement: none
created: 2026-10-21
completed: 2026-10-21
---
# BL-2905 — Expose Curl_get_pathname and Curl_ssh_range counterparts in Curl.Protocol.Ssh to the unit-program ports, for unit2604 and unit2605

## Goal

Curl's counterparts of `Curl_get_pathname` (the SFTP quote word reader, in `SftpQuoteCommand`) and `Curl_ssh_range` (in `SftpDownloadPart`) can be called on their own from the unit-program ports, so the ports of `tests/unit/unit2604.c` and `unit2605.c` (BL-2845) pass.

## Context

unit2604: quoted and unquoted words, the escapes `\"`, `\'` and `\\`, an unknown escape or an unclosed quote is `CURLE_QUOTE_ERROR`, `/~/` expands to the home directory, a word of 90720 bytes is `CURLE_TOO_LARGE`, and the rest after the word. unit2605: start and size for `a-b`, `-n` and `a-`, with `CURLE_RANGE_ERROR` for `-0`, `--2`, a leading or trailing space, `3-2`, `101-102` and `100-` on a 100-byte file. Sources: https://github.com/curl/curl/blob/curl-8_22_0/lib/vssh/vssh.c and the two unit files. Expose each as a function returning curl's code and its outputs.

## Acceptance criteria

- [x] A `Curl_get_pathname` counterpart gives every row of unit2604.c (code, word, rest), and a `Curl_ssh_range` counterpart every row of unit2605.c (code, start, size).
- [x] `Curl.Conformance.UnitProgram.UnitLibrary` can call the counterpart (public, or `InternalsVisibleTo Include="Curl.Conformance.UnitProgram.UnitLibrary"` in its library's csproj), so BL-2845 can port the program without touching this library.
- [x] A focused unit test in the library's own `.UnitTests` project pins every row of the C program's table (ADR-0482).
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and the changed library keeps 100% line and branch coverage and complexity at most 10.

## Notes

- Counterparts: `SftpQuoteCommand.ReadPathname(value, homeDirectory)` returns `(CurlExitCode Result, byte[] Path, string Remainder)`, the same `ArgumentReader` `-Q` parsing uses; `SftpDownloadPart.ReadRange(rangeText, fileSize)` returns `(CurlExitCode Result, long Start, long Size)` over the existing `Curl_ssh_range` text reader. Both internal; the csproj adds `InternalsVisibleTo Curl.Conformance.UnitProgram.UnitLibrary` (BL-2845 still adds its own project reference).
- Read against curl-8_22_0's lib/vssh/vssh.c: the reader now returns curl's code and adds `MAX_PATHLENGTH` (65535): a path of 65535 bytes or more is `CURLE_TOO_LARGE`, a quoted one as the character is added (dynbuf's limit). For a quoted path the limit counts UTF-16 characters, the same as bytes for ASCII.
- `-Q` parsing still turns every pathname failure into exit 21, so a path of 65535+ bytes there is now 21 (it was accepted); curl returns 100. Filed BL-2915 to measure and match that.
- Tests: `SshPathnameAndRangeTests` pins all 21 rows of unit2604 and 26 of unit2605, plus the limit at 65534/65535. Measure-CodeQuality: Curl.Protocol.Ssh.UnitLibrary 100% line, 100% branch, 0 failing members (`ReadQuoted` split after a first run measured it at 12).

## Log

- 2026-10-21: Created (split from BL-2845, which waits on it).
- 2026-10-21: Backlog -> Doing. Criteria at claim: 19cda1db 3d228e1f d3a9ab15 099d1d27.
- 2026-10-21: Doing -> Done. Curl_get_pathname and Curl_ssh_range counterparts (SftpQuoteCommand.ReadPathname, SftpDownloadPart.ReadRange) give every row of unit2604 and unit2605, visible to Curl.Conformance.UnitProgram.UnitLibrary
