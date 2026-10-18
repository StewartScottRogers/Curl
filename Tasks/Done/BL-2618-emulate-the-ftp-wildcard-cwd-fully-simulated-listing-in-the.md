---
id: BL-2618
title: Emulate the FTP wildcard CWD fully_simulated listing in the case runner so cases 574, 575, 1113, 1162 and 1163 run
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.UnitLibrary, Curl.Conformance.UnitTests]
requirement: none
created: 2026-10-17
completed: 2026-10-18
---
# BL-2618 — Emulate the FTP wildcard CWD fully_simulated listing in the case runner so cases 574, 575, 1113, 1162 and 1163 run

## Goal

The FTP stand-in in the upstream case runner carries out `CWD fully_simulated` and its wildcard listing, so cases 574, 575, 1113, 1162 and 1163 run instead of being skipped.

## Context

Found by BL-2532, which ported lib574 and lib575. `UpstreamCaseScreening.FtpDataConnectionCommand` skips any case whose protocol holds `CWD fully_simulated`; upstream's `tests/ftpserver.pl` (curl 8.22.0) answers it with a fixed UNIX LIST and files. BL-1906 (Deferred) noted it as not emulated.

## Acceptance criteria

- [x] Each case that passes through the runner on Windows is added to `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`; one that still fails is filed with the board script with its differing lines.
- [x] No added case's output depends on the platform.
- [x] `dotnet build` is clean and the fast tests pass with no test deleted, skipped or weakened.

## Notes

- Plan: new `FtpSimulatedDirectories` holds `tests/directories.pm`'s UNIX and DOS listings (the bytes `FtpFileListParserTests` already pins) and file contents; `FtpControlChannelResponder` tells `FtpTransferCommands.ChangeDirectory` where `CWD` left it, and under `/fully_simulated` `LIST`, `RETR` and `SIZE` answer from those lists, as ftpserver.pl's `$ftplistparserstate` does. Screening no longer skips `CWD fully_simulated`; its skip row became a test that such a case runs.
- Choice: a `RETR` of a name the simulated list lacks answers nothing, as ftpserver.pl's `RETR_ftp` sends nothing in that branch; `/fully_simulated/` itself lists nothing (no case lists it, and its listing bytes are pinned nowhere here).
- Measured on Windows: 574, 1113, 1162 and 1163 pass and are on the ratchet. 575 runs and differs in the `Libcurl` engine: `<verify><protocol> differs at byte 400 (line 38): expected "QUIT\r\n", got "EPSV\r\n"` (the duplicated handle reuses the first connection where libcurl sends `QUIT` and logs in again); filed as BL-2722.
- Platform check: 574 and 1113 use lib574's own fnmatch callback. 1162 (`[*\s-'tl`) and 1163 (`*[][`) go through the platform matcher: a throwaway check of `FtpWildcardPattern.Match` over every listed name gives NoMatch for Curl and Linux and Fail for macOS, and `FtpSession` keeps only a Match, so both end with no file and exit 78 everywhere. Listing and file bytes are fixed strings.
- Also: fixed the garbled summary line in `FtpTransferCommands`' doc comment, and split `Retrieve` (complexity 12 after the change) into `RetrieveTestFile`. Measure-CodeQuality for Curl.Conformance.UnitLibrary: 100% line, 100% branch, 0 failing members, worst CRAP 10.

## Log

- 2026-10-17: Created.
- 2026-10-18: Backlog -> Doing. Criteria at claim: 57abe33b 21e5ddeb 4272a6b9.
- 2026-10-18: Doing -> Done. The FTP stand-in serves ftpserver.pl's fully_simulated wildcard directories; upstream cases 574, 1113, 1162 and 1163 pass on the ratchet, 575 filed as BL-2722
