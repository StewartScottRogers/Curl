---
id: BL-5114
title: Name /C:name in the FILE open-failure message for a drive-relative file URL
priority: Low
assignee: Claude
pipeline: protocol
depends-on: []
touches: [Curl.Original.Protocol.File.UnitLibrary/FileProtocolHandler.cs, Curl.Original.Protocol.File.UnitTests/FileDriveRelativeOpenFailureMessageTests.cs]
requirement: none
created: 2026-11-13
completed:
---
# BL-5114 — Name /C:name in the FILE open-failure message for a drive-relative file URL

## Goal

The FILE protocol's "Could not open file" message for a drive-relative `file:///C:name` URL names `/C:name`, exactly as upstream curl does, instead of `C:name`.

## Context

From the outside fitness review of 2026-11-13 (commit 56cb97ede, `preview` build), finding STD-064 (Low, Confirmed twice). Message only; the exit code already matches. Area: FILE, Windows.

**Repro.** `curl -sS file:///C:nofile.txt`

**Expected (upstream curl on Windows).** `curl: (37) Could not open file /C:nofile.txt`, exit 37.

**Actual.** `curl: (37) Could not open file C:nofile.txt`, exit 37.

**Where to start.** `Curl.Original.Protocol.File.UnitLibrary/FileProtocolHandler.cs`, where the open-failure message is built.

## Acceptance criteria

- [ ] New tests in `Curl.Original.Protocol.File.UnitTests/FileDriveRelativeOpenFailureMessageTests.cs` prove that `file:///C:nofile.txt` produces exit 37 with message `Could not open file /C:nofile.txt`.
- [ ] Regression tests prove the message for an ordinary absolute path (`file:///dir/nofile.txt` gives `Could not open file /dir/nofile.txt`) and for a full drive path (`file:///C:/nofile.txt`) is unchanged.
- [ ] Tests are platform-neutral where possible; any test whose behaviour depends on drive-letter handling is marked `[OSCondition(OperatingSystems.Windows)]`, with the other platform's answer pinned in its own test (CLAUDE.md).
- [ ] `dotnet build` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Mock servers must be PowerShell, a C# file-based app or `Record-CurlExchange.ps1`, never Python (CLAUDE.md).
- Review acceptance check, as a hand check on Windows: the command prints `curl: (37) Could not open file /C:nofile.txt`.

## Log

- 2026-11-13: Created.
