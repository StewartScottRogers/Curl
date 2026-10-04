---
id: BL-1409
title: Decide how a source time past year 9999 reaches -R, so the Windows build's 'Capping set filetime to max' cap can be matched
priority: Normal
assignee: Claude
pipeline: docs
depends-on: [BL-1392]
touches: [Documentation/Planning/Decisions]
requirement: FR-011
created: 2026-10-03
completed:
---
# BL-1409 — Decide how a source time past year 9999 reaches -R, so the Windows build's 'Capping set filetime to max' cap can be matched

## Goal

An ADR, "Decided by Claude under Stewart's delegation", says how Curl carries a remote file time later than 9999-12-31T23:59:59Z from the protocol handlers to `-R`/`--remote-time` (a `DateTimeOffset` cannot hold it), so the Windows build's maximum cap and the POSIX build's handling can be matched, and files the implementation tasks.

## Context

- Measured 2026-10-03 with curl 8.21.0 (mingw, Schannel), `--no-progress-meter -R -o <file>` against `Last-Modified: Mon, 01 Jan 40000 00:00:00 GMT`: stderr `Warning: Capping set filetime to max to avoid overflow`, the file stamped 30827-12-31T23:59:59Z, exit 0. curl 8.21.0 `src/tool_filetime.c` lines 94-98 cap a Unix time above 910670515199 (30827-12-31T23:59:59Z) on Windows; the POSIX branch (`utimes`, line 128 on) passes the time on, and its failure text is `Failed to set filetime %ld on '%s': %s`.
- Today the time travels as `DateTimeOffset?` (`TransferResult`'s last-modified time in `Curl.Protocol.Abstractions.UnitLibrary`, read by `Curl.Protocol.Http.UnitLibrary/HttpLastModified.cs`, FTP `MDTM`, file and SFTP stat), whose maximum is year 9999, so such a header is read as an unknown time and the file is not stamped at all. BL-1392 adds only the minimum cap and leaves this case to this decision (its Notes).
- Things the ADR must settle: the type that carries the time (for example Unix seconds as `long`), which handlers can produce times past 9999 (HTTP `Last-Modified`, FTP `MDTM`, SFTP and file stat), how `-z` compares such a time, and what the POSIX build does when `utimes` is given one (cite the source, or measure on Linux where a lane can).

## Acceptance criteria

- [ ] A new ADR under `Documentation/Planning/Decisions/` marked "Decided by Claude under Stewart's delegation" settles the points above, names every project that changes (all existing in `Curl.slnx`) and keeps the `Curl.Protocol.Abstractions.UnitLibrary` change to one small task.
- [ ] The implementation tasks are filed with `task-board.ps1 new` in dependency order, each with exact `touches` and criteria that pin the measured Windows output above.
- [ ] `Documentation/Planning/Decisions/README.md` lists the ADR if it keeps an index.

## Notes

- No code changes in this task. Depends on BL-1392 so the minimum cap's shape is known first.

## Log

- 2026-10-03: Created.
