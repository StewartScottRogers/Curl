---
id: BL-3504
title: Fix SMB download with -o writing no bytes
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Smb.UnitLibrary, Curl.Protocol.Smb.UnitTests, Curl.Conformance.UnitTests/CurlTestCases/Extended/SMB/ext11005.curltest]
requirement: none
created: 2026-10-25
completed: 2026-10-25
---
# BL-3504 — Fix SMB download with -o writing no bytes

## Goal

Found by BL-3485: an SMB download with `-o <file>` (or `-o /dev/null -w %{size_download}`) writes no bytes and reports size_download 0, while the same download to stdout works. Reproduce with Extended case: `-u curltest:curltest smb://%HOSTIP:%SMBPORT/TESTS/%TESTNUMBER -o %LOGDIR/out` and `<file name="%LOGDIR/out">` holding the reply data. Fix it, add a unit test in Curl.Protocol.Smb.UnitTests, and add that case to Extended/SMB as ext11005.

<!-- One sentence: the observable outcome once this task is done. -->

## Context

<!-- Why this matters, and where to start: requirement IDs, ADRs, projects, files, upstream curl links with the curl version. -->

## Acceptance criteria

- [x] <!-- A statement someone else can check from the repository without asking a question. -->

## Notes

- Criteria at claim held only the template line, so it is ticked unchanged (rewording would fail the claim fingerprint); what was checked: ext11005 (`-o %LOGDIR/out`, `<file name="%LOGDIR/out">`) passes in `ExtendedCases_RunThroughCurl_AllPass`; `SmbFileTransferTests.TransferAsync_OutputThatCannotSeek_ReceivesTheFilesBytesAndCountsThem` pins a download writing its bytes to an output that cannot seek, as `-o`'s file and stdout are; build clean and fast tests green on Windows, and the change adds no path, OS error or certificate, so it is platform-neutral for CI on Linux and macOS.
- 2026-10-25 (lane 2): No SMB bug. Reproduced with ext11005 as BL-3504 described it: stderr held `Warning: Got more output options than URLs` and the bytes were in `%LOGDIR/curl11005.out`. The runner puts `--output %LOGDIR/curl%TESTNUMBER.out` before every command, as upstream's runner.pm does unless `<command option="no-output">`, so the URL took that `--output` and the case's own `-o` was left over - real curl does the same. Upstream's cases that name their own `-o` carry `option="no-output"` (test1129 and on); with it ext11005 passes and nothing in `Curl.Protocol.Smb.UnitLibrary` changed. The `-o /dev/null -w %{size_download}` symptom of BL-3485 has the same cause. Measure-CodeQuality was not run: no production code changed.
- 2026-10-25 (lane 2): Added `Curl.Conformance.UnitTests/CurlTestCases/Extended/SMB/ext11005.curltest` to touches (the Goal names it); no task in Doing on origin/work/dark-factory names that folder.
- Expected bytes are the stand-in's `<reply><data>`, as ext11001-ext11004: the reference curl 8.22.0 (Schannel) has no SMB to measure.

## Log

- 2026-10-25: Created.
- 2026-10-25: Backlog -> Doing. Criteria at claim: fbecace0.
- 2026-10-25: Doing -> Done. ext11005 passes: the case needed option=no-output; SMB writes -o bytes, unit test pins it
