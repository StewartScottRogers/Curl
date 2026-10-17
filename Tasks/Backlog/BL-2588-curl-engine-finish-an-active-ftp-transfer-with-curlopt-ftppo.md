---
id: BL-2588
title: Curl.Engine: finish an active FTP transfer with CURLOPT_FTPPORT - and socket callbacks (test596 hangs)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitLibrary, Curl.Engine.UnitTests]
requirement: none
created: 2026-10-17
completed:
---
# BL-2588 — Curl.Engine: finish an active FTP transfer with CURLOPT_FTPPORT - and socket callbacks (test596 hangs)

## Goal

An active-mode FTP download through Libcurl (`CURLOPT_FTPPORT` `-`) with open and close socket callbacks finishes, as in test596.

## Context

Found by BL-2527 running test596 through the ported `lib500` (third argument `activeftp`, socket callbacks on for cases 585, 586, 595 and 596): the run does not finish within 20 seconds, while the passive test595 passes. Look at the data connection the engine's `SocketCallbackConnector` wraps for `EPRT` and how the case's in-memory FTP server connects back.

## Acceptance criteria

- [ ] test596 passes through the runner and is added to `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`.
- [ ] A focused unit test in the fixed library's own `.UnitTests` project pins the fix (ADR-0482).
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-10-17: Created.
