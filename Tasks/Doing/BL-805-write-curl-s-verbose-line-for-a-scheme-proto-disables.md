---
id: BL-805
title: Write curl's verbose line for a scheme --proto disables
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-523]
touches: [Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-805 — Write curl's verbose line for a scheme --proto disables

## Goal

Under `-v`, a URL whose scheme `--proto` excludes writes `* Protocol "http" is disabled` to stderr before the `curl: (1) Protocol "http" is disabled` line, as curl 8.21.0 does.

## Context

- Found while doing BL-523. Measured with `Record-CurlExchange.ps1` against Windows curl 8.21.0 on 2026-09-28:
  `curl -v --proto -http http://127.0.0.1:48523/` exits 1 with stderr
  `* Protocol "http" is disabled` CRLF `curl: (1) Protocol "http" is disabled` CRLF, and opens no connection.
- BL-523 made `ProtocolDispatcher` refuse the scheme (`Curl.Core.UnitLibrary/ProtocolDispatcher.cs`); the verbose line is not written today.
- Check first whether the `(in redirect)` refusal and `Protocol "x" not supported` also have a verbose line in curl, and cover them the same way if they do.

## Acceptance criteria

- [ ] Measured with `Record-CurlExchange.ps1` for `-v --proto -http`, `-v --proto =http bogus://...` and `-v -L` with `Location: file:///dir/x`; stderr copied into Notes.
- [ ] `Curl.Console.UnitTests` pin the verbose stderr for each measured case byte for byte (platform-neutral, drive-less `file:///dir/x`).
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Console` reports 100% line and branch coverage and no failing member.

## Notes

## Log

- 2026-09-28: Created.
- 2026-10-01: Backlog -> Doing.
