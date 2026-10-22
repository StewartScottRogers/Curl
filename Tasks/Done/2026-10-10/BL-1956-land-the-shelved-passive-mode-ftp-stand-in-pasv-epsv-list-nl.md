---
id: BL-1956
title: Land the shelved passive-mode FTP stand-in (PASV, EPSV, LIST, NLST, RETR) at full coverage
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-1905]
touches: [Curl.Conformance.UnitLibrary, Curl.Conformance.UnitTests]
requirement: none
created: 2026-10-10
completed: 2026-10-10
---
# BL-1956 — Land the shelved passive-mode FTP stand-in (PASV, EPSV, LIST, NLST, RETR) at full coverage

## Goal

BL-1906's finished passive-mode FTP stand-in is committed: about 171 more upstream FTP cases are measured, and Curl.Conformance.UnitLibrary stays at 100% line and branch coverage.

## Context

BL-1906 is deferred because it hit the factory's per-task cost cap twice. Its code is finished, green and shelved in stash 9a4c35992 ("darkfactory BL-1906 20261009-213949"). Apply it by hash (`git stash apply 9a4c35992`), never pop, and resolve any conflict with what landed since (the SSH stand-in, BL-1953/BL-1954/BL-1917/BL-1918). Read BL-1906's Notes (Tasks/Deferred) for what the code does and the decisions it made. Keep this run lean. The code is done, so the work is to apply it, rebuild, run the tests, measure coverage, close any gaps, update CLAUDE.md and commit. Do not rewrite or widen it.

## Acceptance criteria

- [x] FtpTransferCommands, FtpDataConnection and the FtpServerConnector passive-port changes from the stash are committed, and the upstream cases they make pass are on PassingUpstreamCases.txt.
- [x] UpstreamCaseScreening no longer gives the cases a skip reason of the form "the harness has no value for %FTPPORT"; a screening test in Curl.Conformance.UnitTests pins this.
- [x] `powershell -NoProfile -File Measure-CodeQuality.ps1` shows Curl.Conformance.UnitLibrary at 100% line and branch coverage and complexity of at most 10 per method.
- [x] `dotnet build -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` is green.
- [x] Curl.Conformance.UnitLibrary\CLAUDE.md states what the runner now does for passive-mode FTP.

## Notes

- Applied stash 9a4c35992 by hand (FtpServerConnector and FtpControlChannelResponder applied cleanly; screening, its tests, the passing list and CLAUDE.md conflicted with what landed since and were merged by hand; the three new files taken from the stash's untracked tree).
- Choice: the passive port moved from 8995 to 9005. BL-1909 gave 8995 to `%SMTPPORT` meanwhile, and `SmtpServerConnector` sits outside `FtpServerConnector`, so a data connection to 8995 would have reached the SMTP stand-in. 9005 is used by no stand-in; the PASV reply is now `(127,0,0,1,35,45)`. No case verifies the port, so no ADR: ftpserver.pl picks any free port.
- The first Measure-CodeQuality run found 99.43% branch coverage and six members over the IL complexity gate (TryAnswer at 44: the string `switch` compiles to hash branches; `[.. ]` spreads into strings and `All(char.IsAsciiDigit)` chains added unreachable branches). Fixed by a handler table for TryAnswer, span searches (`IndexOfAnyInRange`, `ContainsAnyExceptInRange`) for the digit helpers, small extracted helpers (RetrievableData, TryFindSize, IsNegativeNumber, ModificationTimeAnswer, AnswerTransferOrNotDealtWith) and one test row (an empty `<data>` without `sendzero`). Final run: Curl.Conformance.UnitLibrary 100% line, 100% branch, worst CRAP 10, 0 failing members.
- Curl.Conformance.UnitTests: 2431 passed, 0 failed (the 171 cases listed). Solution `dotnet build -warnaserror` clean; fast tests green.

## Log

- 2026-10-10: Created.
- 2026-10-10: Backlog -> Doing.
- 2026-10-10: Doing -> Done. Passive-mode FTP stand-in (PASV, EPSV, RETR, LIST, NLST, SIZE, MDTM, REST) landed; 171 more upstream FTP cases pass; Curl.Conformance.UnitLibrary at 100% line and branch coverage
