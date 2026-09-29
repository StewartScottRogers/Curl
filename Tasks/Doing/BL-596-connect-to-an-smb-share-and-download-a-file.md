---
id: BL-596
title: Connect to an SMB share and download a file
priority: Low
assignee: Claude
pipeline: protocol
depends-on: [BL-595]
touches: [Curl.Protocol.Smb.UnitLibrary, Curl.Protocol.Smb.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-596 — Connect to an SMB share and download a file

## Goal

`smb://host/share/path` connects to the share (tree connect), opens the file, reads it and writes its bytes, with a missing share or file mapped to curl 8.21.0's exit code and message.

## Context

- Conformance audit 2026-09-28, row 39. Builds on BL-595; dialect and messages: BL-594's ADR.
- Measure against a local Samba server as in BL-595: a file, a missing file, a missing share, a directory.

## Acceptance criteria

- [ ] Measured first as above; stdout bytes, stderr and exit code copied into Notes.
- [ ] `Curl.Protocol.Smb.UnitTests` pin the tree-connect, open and read requests and the output and outcome for each case.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Protocol.Smb.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

## Log

- 2026-09-28: Created.
- 2026-09-29: Backlog -> Doing.
