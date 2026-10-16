---
id: BL-2479
title: FTP: match wildcards with the Linux and macOS builds' system fnmatch where unit1307 says it differs
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2476]
touches: [Curl.Protocol.Ftp.UnitLibrary, Curl.Protocol.Ftp.UnitTests]
requirement: none
created: 2026-10-16
completed:
---
# BL-2479 — FTP: match wildcards with the Linux and macOS builds' system fnmatch where unit1307 says it differs

## Goal

Off Windows, `FtpWildcardPattern` answers as the Linux and macOS curl builds do, which call the system `fnmatch(3)` (`HAVE_FNMATCH`) instead of curl's own matcher.

## Context

BL-2476 ported curl 8.22.0's own `Curl_fnmatch` (`lib/curl_fnmatch.c`) and pinned the custom column of `tests/unit/unit1307.c` on every platform (ADR-0612). Upstream's Linux (glibc) and macOS builds answer differently for the rows marked `LINUX_*` and `MAC_*` in `unit1307.c`: e.g. `[` matches `[` and `[]` matches `[]` on Linux, `\` does not match `\` on Linux, `[[:foo:]]` against `f]` does not match on Linux, and `[\xFF]`-style sets and several bracket forms return `CURL_FNMATCH_FAIL` (no match) on Linux or macOS. Pass the platform in as `FtpFileListParser` does (`runsOnWindows`-style parameters, so both answers are covered on Windows), and pin each platform's answer in its own data rows. Measure real curl's wildcard download on Linux (WSL, `Record-CurlExchange.ps1 -Ftp -ListenAddress`) for one differing pattern before pinning.

## Acceptance criteria

- [ ] Unit tests pin every `LINUX_*` row of `unit1307.c` with the Linux answer and every `MAC_*` row with the macOS answer, and the custom answer stays pinned for Windows.
- [ ] `Measure-CodeQuality.ps1 -Library Curl.Protocol.Ftp.UnitLibrary` reports 0 failing members.
- [ ] ADR-0612 gains an addendum recording the platform split.

## Notes

## Log

- 2026-10-16: Created.
