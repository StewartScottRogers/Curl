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
completed:
---
# BL-2476 — FTP: parse LIST replies into file entries and match names as curl_fnmatch does

## Goal

`Curl.Protocol.Ftp.UnitLibrary` turns an FTP `LIST` reply into file entries carrying every field of libcurl's `struct curl_fileinfo`, and matches file names against a wildcard pattern exactly as upstream's `curl_fnmatch` does.

## Context

Prerequisite of BL-2478 (the FTP wildcard download loop) and so of BL-2420 (CURLOPT_CHUNK_BGN/END and FNMATCH_FUNCTION in the engine). Nothing in the FTP library parses a listing or matches a wildcard today (BL-2420 lane, 2026-10-16). Port upstream curl 8.22.0's `lib/ftplistparser.c` (Unix `ls -l` and Windows/DOS listing formats, file type, permissions, hard links, user, group, size, time text, symlink target) and `lib/curl_fnmatch.c` (`*`, `?`, `[...]` sets with ranges, `!`/`^` negation and `[:alpha:]`-style classes, backslash escapes). The entry type lives in the FTP library (protocols never reference `Curl.Api`); the engine maps it to `CurlFileInfo` in BL-2420. Pure computation, unit tests only.

## Acceptance criteria

- [ ] Unit tests pin the parser on upstream's test 1500-series listing samples (Unix and DOS formats, symlinks, devices) and on a malformed line, which fails as upstream does (`CURLE_FTP_BAD_FILE_LIST`, exit 87).
- [ ] Unit tests pin the matcher against the cases of upstream's `tests/unit/unit1307.c`.
- [ ] `dotnet build` is clean and the fast tests pass.
- [ ] `Measure-CodeQuality.ps1 -Library Curl.Protocol.Ftp.UnitLibrary` reports 0 failing members.
- [ ] Every new public member has an XML doc comment.

## Notes

## Log

- 2026-10-16: Created.
