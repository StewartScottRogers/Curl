---
id: BL-2480
title: FTP: match wildcards with the Linux and macOS builds' system fnmatch where unit1307 says it differs
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2476]
touches: [Curl.Protocol.Ftp.UnitLibrary, Curl.Protocol.Ftp.UnitTests]
requirement: none
created: 2026-10-16
completed: 2026-10-16
---
# BL-2479 — FTP: match wildcards with the Linux and macOS builds' system fnmatch where unit1307 says it differs

## Goal

Off Windows, `FtpWildcardPattern` answers as the Linux and macOS curl builds do, which call the system `fnmatch(3)` (`HAVE_FNMATCH`) instead of curl's own matcher.

## Context

BL-2476 ported curl 8.22.0's own `Curl_fnmatch` (`lib/curl_fnmatch.c`) and pinned the custom column of `tests/unit/unit1307.c` on every platform (ADR-0613). Upstream's Linux (glibc) and macOS builds answer differently for the rows marked `LINUX_*` and `MAC_*` in `unit1307.c`: e.g. `[` matches `[` and `[]` matches `[]` on Linux, `\` does not match `\` on Linux, `[[:foo:]]` against `f]` does not match on Linux, and `[\xFF]`-style sets and several bracket forms return `CURL_FNMATCH_FAIL` (no match) on Linux or macOS. Pass the platform in as `FtpFileListParser` does (`runsOnWindows`-style parameters, so both answers are covered on Windows), and pin each platform's answer in its own data rows. Measure real curl's wildcard download on Linux (WSL, `Record-CurlExchange.ps1 -Ftp -ListenAddress`) for one differing pattern before pinning.

## Acceptance criteria

- [x] Unit tests pin every `LINUX_*` row of `unit1307.c` with the Linux answer and every `MAC_*` row with the macOS answer, and the custom answer stays pinned for Windows.
- [x] `Measure-CodeQuality.ps1 -Library Curl.Protocol.Ftp.UnitLibrary` reports 0 failing members.
- [x] ADR-0613 gains an addendum recording the platform split.

## Notes

- New public `FtpWildcardMatcher` (`Curl`, `Linux`, `MacOs`), `FtpWildcardPattern.Match(pattern, name, matcher)`
  returning `FtpNameMatchAnswer` (so `CURL_FNMATCH_FAIL` is pinned apart from no match) and
  `FtpWildcardPattern.MatcherFor(runsOnWindows, runsOnMacOs)`. `FtpDownloadEveryMatch` takes a matcher;
  `Instance` uses the running platform's. `Matches` stays curl's own matcher. Internal: `FtpSystemFnmatch`
  (UTF-8 decoding, stars with one backtrack point), `FtpGlibcBracket` (ported from glibc's
  `posix/fnmatch_loop.c`), `FtpAppleBracket` (FreeBSD `rangematch`, malformed = fail),
  `FtpWideCharacterClass`.
- `FtpWildcardPatternTests`: every single-answer row of unit1307.c now runs on all three matchers; the 11 rows
  marked `LINUX_*`/`MAC_*` give each platform's answer (`Match_RowsWherePlatformsDiffer_...`, plus the long
  `[[[` row and the fuzzed row as their own tests).
- Measured: the curl tool has no `CURLOPT_WILDCARDMATCH` option, so curl's wildcard download cannot be measured
  from the command line (libcurl only, upstream test 576 is a libtest). Instead glibc 2.43 (WSL Ubuntu,
  `C.UTF-8`) was measured through `find -name`, which calls `fnmatch`: `[` matches `[`, `[]` matches `[]`, `\`
  does not match `\`, `[[:foo:]]` does not match `f]`, all as modelled. No macOS machine: the macOS model is
  inferred from FreeBSD's source plus unit1307's `MAC_*` rows (ADR-0613 addendum).
- Locale: UTF-8, since the curl tool calls `setlocale(LC_ALL, "")` and unit1307's Linux `[\xFF]` FAIL rows only
  hold in a multibyte locale.
- Quality: 100% line, 100% branch, 0 failing members, worst CRAP 10 (524 members).
- Follow-up: BL-2482 (glibc `[=a=]` and `[.a.]` in brackets).

## Log

- 2026-10-16: Created.
- 2026-10-16: Backlog -> Doing.
- 2026-10-16: Doing -> Done. FtpWildcardPattern.Match answers as glibc fnmatch on Linux and macOS libc fnmatch on macOS, pinned on every LINUX_* and MAC_* row of unit1307.c, curl's own matcher kept on Windows
