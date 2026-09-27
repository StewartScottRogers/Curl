---
id: BL-361
title: Print the referer last sent under -e ;auto -L as %{referer}
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-305]
touches: [Curl.Protocol.Abstractions.UnitLibrary, Curl.Protocol.Abstractions.UnitTests, Curl.Output.UnitLibrary, Curl.Output.UnitTests, Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-09-27
completed:
---
# BL-361 — Print the referer last sent under -e ;auto -L as %{referer}

## Goal

`-w "%{referer}"` prints the `Referer` the last request of a transfer was sent with, so under `-e "…;auto" -L` it is the URL the final redirect came from, as curl 8.21.0 prints it.

## Context

- BL-305 (ADR-0060) sets `TransferWriteOutVariables.Referer` from the `-e` value in `CurlCommandRunner.WriteOutAsync`; with `;auto` and `-L`, curl's `CURLINFO_REFERER` is the referer of the last request, which the runner does not know.
- The redirect follower (or the HTTP report) knows the sent `Referer`; carrying it needs a place to record it, likely a `TransferReport` member (ADR-0015 governs what the report carries).
- Measure first with curl 8.21.0 (mingw, Schannel): `-e ";auto" -L` and `-e "http://r/;auto" -L` through one and two redirects, and without `-L`.

## Acceptance criteria

- [ ] The measured `%{referer}` for each case in Context is pinned in `CurlCommandRunnerWriteOutTests`, with the commands and bytes in this task's Notes.
- [ ] `dotnet build -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage for every touched library.

## Notes

## Log

- 2026-09-27: Created.
- 2026-09-27: Backlog -> Doing.
