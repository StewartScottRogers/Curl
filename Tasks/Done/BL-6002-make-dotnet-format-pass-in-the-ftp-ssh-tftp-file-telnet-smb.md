---
id: BL-6002
title: Make dotnet format pass in the FTP, SSH, TFTP, FILE, TELNET, SMB, MQTT, Gopher and DICT protocol projects
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Original.Protocol.Ftp.UnitLibrary, Curl.Original.Protocol.Ftp.UnitTests, Curl.Original.Protocol.Ssh.UnitLibrary, Curl.Original.Protocol.Ssh.UnitTests, Curl.Original.Protocol.Tftp.UnitTests, Curl.Original.Protocol.File.UnitTests, Curl.Original.Protocol.Telnet.UnitTests, Curl.Original.Protocol.Smb.UnitTests, Curl.Original.Protocol.Mqtt.UnitTests, Curl.Original.Protocol.Gopher.UnitTests, Curl.Original.Protocol.Dict.UnitTests]
requirement: none
created: 2026-11-26
completed: 2026-11-26
---
# BL-6002 — Make dotnet format pass in the FTP, SSH, TFTP, FILE, TELNET, SMB, MQTT, Gopher and DICT protocol projects

## Goal

`dotnet format Curl.slnx --verify-no-changes --include <each path in touches>` exits 0: the `using` order, whitespace and final newlines in this task's touches follow `.editorconfig`.

## Context

Part of BL-5923, split by project group so lanes run the parts in parallel. On 2026-11-26 `dotnet format` reported 680 files across 53 projects: 636 IMPORTS (`using` order), 31 FINALNEWLINE and 13 WHITESPACE. Line endings are left to git (ADR-1417), so there are no ENDOFLINE fixes. Run `dotnet format Curl.slnx --include <each path in touches>`, and change nothing outside touches. It is a formatting-only change; if `dotnet format` wants to change code beyond `using` order and whitespace, leave that file alone and say why under Notes.

## Acceptance criteria

- [x] `dotnet format Curl.slnx --verify-no-changes --include <each path in touches>` exits 0.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- `dotnet format` changed 184 files: `using` order, final newlines and whitespace only (three files had
  a `}}` or an unindented statement). No other kind of change, so no file was left out.
- On 2026-11-26 after the format: `--verify-no-changes` exit 0; `dotnet build` 0 warnings, 0 errors;
  fast tests exit 0; `Measure-CodeQuality.ps1` for the FTP and SSH libraries: 0 failing members.
- Platform check: no behaviour or test logic changed, so nothing differs on Linux or macOS.

## Log

- 2026-11-26: Created.
- 2026-11-26: Backlog -> Doing. Criteria at claim: 03521b86 3d836d5e.
- 2026-11-26: Doing -> Done. dotnet format passes in the FTP, SSH, TFTP, FILE, TELNET, SMB, MQTT, Gopher and DICT protocol projects
