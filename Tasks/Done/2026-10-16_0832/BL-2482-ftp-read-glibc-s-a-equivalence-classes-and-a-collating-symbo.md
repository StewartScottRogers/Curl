---
id: BL-2482
title: FTP: read glibc's [=a=] equivalence classes and [.a.] collating symbols in the Linux wildcard matcher
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Ftp.UnitLibrary, Curl.Protocol.Ftp.UnitTests]
requirement: none
created: 2026-10-16
completed: 2026-10-16
---
# BL-2482 — FTP: read glibc's [=a=] equivalence classes and [.a.] collating symbols in the Linux wildcard matcher

## Goal

On Linux, an FTP wildcard pattern holding `[=a=]` or `[.a.]` inside a bracket matches as glibc's `fnmatch` does in a UTF-8 locale.

## Context

BL-2480 ported glibc's `fnmatch` bracket reading (`posix/fnmatch_loop.c`) into `FtpGlibcBracket`, but reads `[=` and
`[.` as an ordinary `[` (ADR-0613, BL-2480 addendum). glibc reads `[=c=]` as every character with `c`'s primary
collation weight and `[.c.]` as the collating element `c`, both in the main loop and when skipping the rest of a
matched bracket. Measure glibc through `find -name` in WSL (`C.UTF-8` and `en_US.UTF-8`; `find` calls `fnmatch`)
before pinning. If FreeBSD's `rangematch` reads them too, give `FtpAppleBracket` the same treatment.

## Acceptance criteria

- [x] `FtpGlibcBracketTests` pins `[[=a=]]`, `[[.a.]]`, `[[.-.]]` and a matched bracket skipping a `[=x=]`, with the answers measured from glibc.
- [x] `Measure-CodeQuality.ps1 -Library Curl.Protocol.Ftp.UnitLibrary` reports 0 failing members.
- [x] ADR-0613's BL-2480 addendum no longer says these are unread.

## Notes

- Measured glibc 2.43 in WSL Ubuntu through `find -name`. Only `C.UTF-8` is installed there (`locale -a`: C,
  C.utf8, POSIX), so `en_US.UTF-8` fell back to C and was not a real measurement. Default taken: read `[=` and `[.`
  as in a locale with no collation rules (C.UTF-8), recorded in ADR-0613's BL-2480 addendum (BL-2482 paragraph).
- Measured quirks the port keeps: `[[=` matches `[[=` (a `[=` at the pattern's end leaves the bracket open);
  `[[.a.]-]` matches `-` but not `a`; `[[=a=]-c]` matches `-`, `c` and `a` but not `b`; `[a[=xy=]]` matches `x]`
  and `[]` but not `a`; `[a-\[.c.]]` reads the escaped `[` literally.
- Not modelled: `find` (which runs its own bundled fnmatch) matched `[]` for `[[=é=]]` byte-wise; the port reads code
  points. `FtpAppleBracket` unchanged: FreeBSD's rangematch reads no `[=`/`[.` and no macOS machine to measure.
- The previous run of this task had only rewritten the class's doc comment; this run measured, ported and tested.

## Log

- 2026-10-16: Created.
- 2026-10-16: Backlog -> Doing.
- 2026-10-16: Doing -> Done. glibc [=c=] and [.c.] read in FtpGlibcBracket, measured from glibc 2.43 C.UTF-8 and pinned; quality gate 0 failing
