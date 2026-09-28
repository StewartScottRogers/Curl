---
id: BL-597
title: Upload a file to an SMB share
priority: Low
assignee: Claude
pipeline: protocol
depends-on: [BL-596]
touches: [Curl.Protocol.Smb.UnitLibrary, Curl.Protocol.Smb.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-597 — Upload a file to an SMB share

## Goal

`-T file smb://host/share/path` creates or truncates the remote file and writes the bytes, as curl 8.21.0 does, with a refused create or write mapped to curl's exit code and message.

## Context

- Conformance audit 2026-09-28, row 39. Builds on BL-596.
- Measure against a local Samba server as in BL-595: a new file, an existing file, a read-only share, `-T -`.

## Acceptance criteria

- [ ] Measured first as above; stderr, exit code and the resulting remote file copied into Notes.
- [ ] `Curl.Protocol.Smb.UnitTests` pin the create and write requests and the outcome for each case; upload progress and `%{size_upload}` match the measurement.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Protocol.Smb.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

## Log

- 2026-09-28: Created.
