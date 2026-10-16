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
completed:
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

- [ ] `FtpGlibcBracketTests` pins `[[=a=]]`, `[[.a.]]`, `[[.-.]]` and a matched bracket skipping a `[=x=]`, with the answers measured from glibc.
- [ ] `Measure-CodeQuality.ps1 -Library Curl.Protocol.Ftp.UnitLibrary` reports 0 failing members.
- [ ] ADR-0613's BL-2480 addendum no longer says these are unread.

## Notes

## Log

- 2026-10-16: Created.
- 2026-10-16: Backlog -> Doing.
