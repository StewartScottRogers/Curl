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
completed:
---
# BL-1956 — Land the shelved passive-mode FTP stand-in (PASV, EPSV, LIST, NLST, RETR) at full coverage

## Goal

BL-1906's finished passive-mode FTP stand-in is committed: about 171 more upstream FTP cases are measured, and Curl.Conformance.UnitLibrary stays at 100% line and branch coverage.

## Context

BL-1906 is deferred because it hit the factory's per-task cost cap twice. Its code is finished, green and shelved in stash 9a4c35992 ("darkfactory BL-1906 20261009-213949"). Apply it by hash (`git stash apply 9a4c35992`), never pop, and resolve any conflict with what landed since (the SSH stand-in, BL-1953/BL-1954/BL-1917/BL-1918). Read BL-1906's Notes (Tasks/Deferred) for what the code does and the decisions it made. Keep this run lean. The code is done, so the work is to apply it, rebuild, run the tests, measure coverage, close any gaps, update CLAUDE.md and commit. Do not rewrite or widen it.

## Acceptance criteria

- [ ] FtpTransferCommands, FtpDataConnection and the FtpServerConnector passive-port changes from the stash are committed, and the upstream cases they make pass are on PassingUpstreamCases.txt.
- [ ] UpstreamCaseScreening no longer gives the cases a skip reason of the form "the harness has no value for %FTPPORT"; a screening test in Curl.Conformance.UnitTests pins this.
- [ ] `powershell -NoProfile -File Measure-CodeQuality.ps1` shows Curl.Conformance.UnitLibrary at 100% line and branch coverage and complexity of at most 10 per method.
- [ ] `dotnet build -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` is green.
- [ ] Curl.Conformance.UnitLibrary\CLAUDE.md states what the runner now does for passive-mode FTP.

## Notes

## Log

- 2026-10-10: Created.
