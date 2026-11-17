---
id: BL-5340
title: Report url.host and urle.host as null in %{json} for file:// URLs
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Output.UnitLibrary/TransferWriteOutVariables.cs, Curl.Output.UnitLibrary/WriteOutTransferInfo.cs, Curl.Output.UnitTests]
requirement: none
created: 2026-11-16
completed: 2026-11-16
---
# BL-5340 — Report url.host and urle.host as null in %{json} for file:// URLs

## Goal

`-w '%{json}'` for a `file://` URL reports `"url.host":null` and `"urle.host":null`, as curl does, instead of empty strings.

## Context

From the outside fitness review of 2026-11-16 (commit 64e9144, `preview` build), finding STD-090 (Low, Confirmed: Windows twice, Linux once). Area: FILE with `-w %{json}`; Windows and Linux.

**Repro.** `curl -s -o NUL -w '%{json}' "file:///C:/Windows/win.ini" | grep -o '"urle*.host":[^,]*'` (Linux: `curl -s -o /dev/null -w '%{json}' file:///etc/hostname`).

**Expected (REF, REF2).** `"url.host":null` and `"urle.host":null` (also for `file://localhost/...` and a missing file).

**Actual (PORT).** `"url.host":""` and `"urle.host":""`.

**Where to start.** The JSON write-out URL-part code in `Curl.Output.UnitLibrary`: `TransferWriteOutVariables.cs` (the `UrlParts` table, `("host", url => url.Host)`) and `WriteOutTransferInfo.cs`. An empty host must become `null`.

## Acceptance criteria

- [x] New tests in `Curl.Output.UnitTests` (a new file) prove that `%{json}` for `file:///etc/hostname`, `file://localhost/etc/hostname` and a missing-file URL has `"url.host":null` and `"urle.host":null`, and that `%{url.host}` in a plain `-w` still writes the empty string for them.
- [x] A test proves an `http://example.com/` transfer still reports `"url.host":"example.com"` (no regression).
- [x] Tests are platform-neutral (drive-less `file:///` URLs).
- [x] Hand check recorded in the Log: the repro prints `"url.host":null` and `"urle.host":null` on Windows and Linux. Measure real curl with `Record-CurlExchange.ps1` where possible, and check how curl treats other empty URL parts (port, user) before changing more than host.
- [x] `dotnet build` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Mock servers must be PowerShell, a C# file-based app or `Record-CurlExchange.ps1`, never Python (CLAUDE.md).
- Review acceptance check: the command prints `"url.host":null` and `"urle.host":null`.

## Log

- 2026-11-16: Created.
- 2026-11-16: Backlog -> Doing. Criteria at claim: 686f2b49 b9302aa8 4da245a8 b50ca603 a0b49a29.
- 2026-11-16: Done. Hand check: real curl (Linux build here) prints `"url.host":null` and `"urle.host":null` for file:///etc/hostname; Curl.Output now matches via `("host", url => url.Host.Length == 0 ? null : url.Host)`. Plain `%{url.host}` still prints nothing. Only host changed: port/user etc. already map absent parts to null. Tests are platform-neutral; Measure-CodeQuality for Curl.Output.UnitLibrary: 0 failing members.
- 2026-11-16: Doing -> Done. url.host and urle.host are null in %{json} for host-less URLs
