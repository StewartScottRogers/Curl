---
id: BL-3504
title: Fix SMB download with -o writing no bytes
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Smb.UnitLibrary, Curl.Protocol.Smb.UnitTests]
requirement: none
created: 2026-10-25
completed:
---
# BL-3504 — Fix SMB download with -o writing no bytes

## Goal

Found by BL-3485: an SMB download with `-o <file>` (or `-o /dev/null -w %{size_download}`) writes no bytes and reports size_download 0, while the same download to stdout works. Reproduce with Extended case: `-u curltest:curltest smb://%HOSTIP:%SMBPORT/TESTS/%TESTNUMBER -o %LOGDIR/out` and `<file name="%LOGDIR/out">` holding the reply data. Fix it, add a unit test in Curl.Protocol.Smb.UnitTests, and add that case to Extended/SMB as ext11005.

<!-- One sentence: the observable outcome once this task is done. -->

## Context

<!-- Why this matters, and where to start: requirement IDs, ADRs, projects, files, upstream curl links with the curl version. -->

## Acceptance criteria

- [ ] <!-- A statement someone else can check from the repository without asking a question. -->

## Notes

## Log

- 2026-10-25: Created.
