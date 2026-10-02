---
id: BL-1215
title: Stop a -d POST to the loopback recorder failing with exit 55 or 56
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests]
requirement: none
created: 2026-10-02
completed:
---
# BL-1215 — Stop a -d POST to the loopback recorder failing with exit 55 or 56

## Goal

`curl -d ab http://127.0.0.1:<port>/` against `Record-CurlExchange.ps1` exits 0 every time, as real curl does.

## Context

- Found in BL-1189: Curl's `-d ab` (and `-d @file` of 100000 bytes) against `Record-CurlExchange.ps1` (`200`, `Content-Length: 2`, `hi`) exits 56 `Failure when receiving data from the peer` or 55 `Failed sending data to the peer` in most runs (3 of 4 for `-d ab`, every run for 100000 bytes), before and after BL-1189's change; real curl 8.21.0 exits 0 every time. `-T` of the same 100000 bytes exits 0. Start by comparing `request.bin` and the write pattern (head and body as separate writes?) with real curl's.

## Acceptance criteria

- [ ] The cause is found and stated in Notes.
- [ ] A test reproducing it fails before the fix and passes after; 10 runs of `-d ab` and of `-d @<100000 bytes>` against the recorder all exit 0.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage for each library changed.

## Notes

## Log

- 2026-10-02: Created.
