---
id: BL-2476
title: FTP: parse LIST replies into file entries and match names as curl_fnmatch does
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Ftp.UnitLibrary, Curl.Protocol.Ftp.UnitTests]
requirement: none
created: 2026-10-16
completed: 2026-10-16
---
# BL-2476 — FTP: parse LIST replies into file entries and match names as curl_fnmatch does

## Goal

`Curl.Protocol.Ftp.UnitLibrary` turns an FTP `LIST` reply into file entries carrying every field of libcurl's `struct curl_fileinfo`, and matches file names against a wildcard pattern exactly as upstream's `curl_fnmatch` does.

## Context

Prerequisite of BL-2478 (the FTP wildcard download loop) and so of BL-2420 (CURLOPT_CHUNK_BGN/END and FNMATCH_FUNCTION in the engine). Nothing in the FTP library parses a listing or matches a wildcard today (BL-2420 lane, 2026-10-16). Port upstream curl 8.22.0's `lib/ftplistparser.c` (Unix `ls -l` and Windows/DOS listing formats, file type, permissions, hard links, user, group, size, time text, symlink target) and `lib/curl_fnmatch.c` (`*`, `?`, `[...]` sets with ranges, `!`/`^` negation and `[:alpha:]`-style classes, backslash escapes). The entry type lives in the FTP library (protocols never reference `Curl.Api`); the engine maps it to `CurlFileInfo` in BL-2420. Pure computation, unit tests only.

## Acceptance criteria

- [x] Unit tests pin the parser on upstream's test 1500-series listing samples (Unix and DOS formats, symlinks, devices) and on a malformed line, which fails as upstream does (`CURLE_FTP_BAD_FILE_LIST`, exit 87).
- [x] Unit tests pin the matcher against the cases of upstream's `tests/unit/unit1307.c`.
- [x] `dotnet build` is clean and the fast tests pass.
- [x] `Measure-CodeQuality.ps1 -Library Curl.Protocol.Ftp.UnitLibrary` reports 0 failing members.
- [x] Every new public member has an XML doc comment.

## Notes

- The listing samples upstream uses are not in a 1500-series test: test574-576 list `/fully_simulated/UNIX/` and
  `/fully_simulated/DOS/`, built by `tests/directories.pm` (curl-8_22_0 tag). `FtpFileListParserTests` feeds those
  exact lines and checks the fields test576 prints (permissions in octal, size, user, group, time text, type,
  link target). Devices: `b`/`c` lines with a plain size parse; a `1,   3` major/minor size is exit 87, as in curl.
- Ported as a byte state machine: `FtpFileListParser` (public: chunks in, `Entries`, `Failure`) over
  `FtpUnixListLineReader` and `FtpDosListLineReader`; `FtpWildcardPattern` (public) with `FtpWildcardSet`.
  Decisions in ADR-0613: curl's own matcher on every platform (system `fnmatch` differences on Linux and macOS
  filed as BL-2480), texts as Latin-1 cut at the first NUL, hard link limit by `runsOnWindows`, a selector
  predicate asked once per entry, an entry past 9,999 bytes is exit 27 (`CURLE_OUT_OF_MEMORY`).
- Quality: 100% line and branch, 0 failing members, worst CRAP 10 (448 members).

## Log

- 2026-10-16: Created.
- 2026-10-16: Backlog -> Doing.
- 2026-10-16: Doing -> Done. FtpFileListParser reads Unix and DOS LIST replies into curl_fileinfo-shaped entries (exit 87 on a malformed line) and FtpWildcardPattern matches as curl's Curl_fnmatch
