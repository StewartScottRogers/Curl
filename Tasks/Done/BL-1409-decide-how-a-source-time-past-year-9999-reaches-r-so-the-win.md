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
completed: 2026-10-03
---
# BL-1409 — Decide how a source time past year 9999 reaches -R, so the Windows build's 'Capping set filetime to max' cap can be matched

## Goal

An ADR, "Decided by Claude under Stewart's delegation", says how Curl carries a remote file time later than 9999-12-31T23:59:59Z from the protocol handlers to `-R`/`--remote-time` (a `DateTimeOffset` cannot hold it), so the Windows build's maximum cap and the POSIX build's handling can be matched, and files the implementation tasks.

## Context

- Measured 2026-10-03 with curl 8.21.0 (mingw, Schannel), `--no-progress-meter -R -o <file>` against `Last-Modified: Mon, 01 Jan 40000 00:00:00 GMT`: stderr `Warning: Capping set filetime to max to avoid overflow`, the file stamped 30827-12-31T23:59:59Z, exit 0. curl 8.21.0 `src/tool_filetime.c` lines 94-98 cap a Unix time above 910670515199 (30827-12-31T23:59:59Z) on Windows; the POSIX branch (`utimes`, line 128 on) passes the time on, and its failure text is `Failed to set filetime %ld on '%s': %s`.
- Today the time travels as `DateTimeOffset?` (`TransferResult`'s last-modified time in `Curl.Protocol.Abstractions.UnitLibrary`, read by `Curl.Protocol.Http.UnitLibrary/HttpLastModified.cs`, FTP `MDTM`, file and SFTP stat), whose maximum is year 9999, so such a header is read as an unknown time and the file is not stamped at all. BL-1392 adds only the minimum cap and leaves this case to this decision (its Notes).
- Things the ADR must settle: the type that carries the time (for example Unix seconds as `long`), which handlers can produce times past 9999 (HTTP `Last-Modified`, FTP `MDTM`, SFTP and file stat), how `-z` compares such a time, and what the POSIX build does when `utimes` is given one (cite the source, or measure on Linux where a lane can).

## Acceptance criteria

- [x] A new ADR under `Documentation/Planning/Decisions/` marked "Decided by Claude under Stewart's delegation" settles the points above, names every project that changes (all existing in `Curl.slnx`) and keeps the `Curl.Protocol.Abstractions.UnitLibrary` change to one small task.
- [x] The implementation tasks are filed with `task-board.ps1 new` in dependency order, each with exact `touches` and criteria that pin the measured Windows output above.
- [x] `Documentation/Planning/Decisions/README.md` lists the ADR if it keeps an index.

## Notes

- No code changes in this task. Depends on BL-1392 so the minimum cap's shape is known first.
- Decided in ADR-0410: times travel as Unix seconds (`TransferResult.SourceLastWriteUnixSeconds`, `TimeCondition.ValueUnixSeconds`), the `DateTimeOffset` properties staying as in-range views so the Abstractions change (BL-1421) is additive and alone. HTTP and `file://` can exceed 9999; FTP `MDTM` (four-digit year) and SFTP/SCP (32-bit mtime) cannot, so they do not change.
- Choice: the POSIX behaviour is taken from curl's source (`utimes` given the time unchanged, no cap) rather than measured, since lanes run on Windows; BL-1425 passes the time on uncapped off Windows to match.
- Filed in dependency order: BL-1421 (Abstractions) -> BL-1422 (HTTP), BL-1423 (file, Low), BL-1424 (Cli `-z` date, Low) -> BL-1425 (`-R` cap, Core + Console, after 1421 and 1422) and BL-1426 (If-Modified-Since text, after 1422 and 1424).
- Docs only: no `.cs` or project file changed, so no build or verify was needed.

## Log

- 2026-10-03: Created.
- 2026-10-03: Backlog -> Doing.
- 2026-10-03: Doing -> Done. ADR-0410 decides times travel as Unix seconds so -R can stamp and cap past year 9999; BL-1421..BL-1426 filed
