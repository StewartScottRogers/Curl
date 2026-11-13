---
id: BL-5047
title: Refuse --webdav-list and --webdav-sync on a non-HTTP URL with exit 1 before connecting
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extension.WebDavTools.UnitLibrary/Transfer/WebDavCollectionListing.cs, Curl.Extension.WebDavTools.UnitTests/Transfer/WebDavListTransferHookTests.cs, Curl.Extension.WebDavTools.UnitTests/Transfer/WebDavSyncTransferHookTests.cs]
requirement: none
created: 2026-11-12
completed: 2026-11-12
---
# BL-5047 — Refuse --webdav-list and --webdav-sync on a non-HTTP URL with exit 1 before connecting

## Goal

`--webdav-list` and `--webdav-sync` on an ftp or file URL exit 1 with `--webdav-list and --webdav-sync need an HTTP or HTTPS URL` and make no connection.

## Context

From the outside fitness review of 2026-11-12 (commit 2d2ad7f8b, `preview` curl-win-x64.zip), finding EXT-039 (Low, Confirmed).

**Repro.** With nothing listening on 48001 run `curl -sS --webdav-list ftp://127.0.0.1:48001/`, `curl -sS --webdav-sync wdx ftp://127.0.0.1:48001/` and `curl -sS --webdav-list file:///C:/Windows/win.ini`.

**Expected.** ADR-1164: "A transfer that is not HTTP fails with exit 1 without contacting the server". The code's own message is `--webdav-list and --webdav-sync need an HTTP or HTTPS URL` (`WebDavCollectionListing.NotHttpMessage`).

**Actual.** For ftp: `curl: (7) Failed to connect to 127.0.0.1:48001 after 1 ms: Could not connect to server`, exit 7 (a connection is attempted). For file: `curl: (22) The server answered PROPFIND with 0, not 207 Multistatus`, exit 22.

**Where to start.** `Curl.Extension.WebDavTools.UnitLibrary/Transfer/WebDavCollectionListing.cs`, `ListAsync`: the `context.Http is null` test does not catch ftp or file transfers. Check the URL scheme (`http` or `https`) instead of, or as well as, `context.Http`. `WebDavListTransferHook.cs` and `WebDavSyncTransferHook.cs` call it.

## Acceptance criteria

- [x] `Curl.Extension.WebDavTools.UnitTests/Transfer/WebDavListTransferHookTests.cs` has tests that an ftp URL and a file URL (use a drive-less `file:///dir/x`) give exit 1 (`CurlExitCode.UnsupportedProtocol`) with message `--webdav-list and --webdav-sync need an HTTP or HTTPS URL`, and that the `next` delegate is never called (no connection).
- [x] `Curl.Extension.WebDavTools.UnitTests/Transfer/WebDavSyncTransferHookTests.cs` has the same test for `--webdav-sync` with an ftp URL.
- [x] Regression: http and https URLs still run PROPFIND as before (the existing list and sync tests pass).
- [x] ADR-1164 (`Documentation/Planning/Decisions/ADR-1164-webdav-tools-list-and-sync-a-collection-through-curls-http-stack.md`) is checked against the behaviour and corrected only if it says anything different.
- [x] `dotnet build` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

Done: ListAsync now also requires an http or https scheme (case-insensitive), so ftp and file URLs with Http options set fail with exit 1 before next runs. ADR-1164 already says this; no change needed. Linux and macOS CI: tests are platform-neutral (drive-less file URL). Quality gate run: 0 failing members.

Mock servers in the review were Python; build stand-ins in PowerShell, a C# file-based app or `Record-CurlExchange.ps1`, never Python (CLAUDE.md). The review's file repro uses a drive-letter URL; tests must use a drive-less `file:///dir/x` (CLAUDE.md, tests pass on Windows, Linux and macOS), since off Windows curl rejects a drive letter with exit 3 before the hook runs.

## Log

- 2026-11-12: Created.
- 2026-11-12: Backlog -> Doing. Criteria at claim: 28ae6b06 6b834cb4 054e53e5 01d72800 a0b49a29.
- 2026-11-12: Doing -> Done. Scheme checked before PROPFIND
