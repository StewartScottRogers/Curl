---
id: BL-1955
title: Read an SSH case's upload after its postcheck in the case runner
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Conformance.UnitLibrary, Curl.Conformance.UnitTests]
requirement: none
created: 2026-10-10
completed:
---
# BL-1955 — Read an SSH case's upload after its postcheck in the case runner

## Goal

Upstream SFTP cases 624 and 625 compare `<verify><upload>` with the file their `<postcheck>` moves into `%LOGDIR/upload.%TESTNUMBER`, as runtests.pl does.

## Context

Found by BL-1918. `UpstreamCaseRunner.SshUpload` reads `%LOGDIR/upload.%TESTNUMBER` right after curl exits, but 624 and 625 upload into `%LOGDIR/test%TESTNUMBER.dir/` (`--ftp-create-dirs`) and their `<postcheck>` (`test610.pl move ...`) moves the file to `upload.%TESTNUMBER` before runtests.pl compares the upload. Today both fail with "<verify><upload> differs at byte 0 ... got the end". Read the upload after the postcheck runs (see `UpstreamCaseVerification` and `UpstreamTest610Script`).

## Acceptance criteria

- [ ] 624 and 625 pass through `UpstreamCaseRunner` and are listed in `Curl.Conformance.UnitTests\PassingUpstreamCases.txt`.
- [ ] A test in `Curl.Conformance.UnitTests` pins that the upload is read after the postcheck.
- [ ] Curl.Conformance.UnitLibrary holds 100% line and branch coverage; `dotnet test --filter "TestCategory!=Integration"` is green.

## Notes

## Log

- 2026-10-10: Created.
- 2026-10-10: Backlog -> Doing.
