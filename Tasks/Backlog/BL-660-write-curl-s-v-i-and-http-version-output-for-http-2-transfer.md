---
id: BL-660
title: Write curl's -v, -i and %{http_version} output for HTTP/2 transfers
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-659]
touches: [Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests, Curl.Output.UnitLibrary, Curl.Output.UnitTests, Curl.Console.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-660 — Write curl's -v, -i and %{http_version} output for HTTP/2 transfers

## Goal

An HTTP/2 transfer writes what curl 8.21.0 writes on each platform (the OpenSSL build on Linux and macOS; on Windows, where the Schannel reference build has no HTTP/2, the build BL-655's ADR names, curl.se's official Windows build): `-i` status line `HTTP/2 200` with lower-case header names, the `-v` lines for ALPN, the stream and the request and response headers, `%{http_version}` `2`, and `-V` listing `HTTP2` among the features on every platform.

## Context

- Conformance audit 2026-09-28, row 32. Standing rule (root `CLAUDE.md`, "Decisions", 2026-09-28): HTTP/2 is offered on every platform; output text matches the platform's curl where both do the same thing.
- Formatting: `Curl.Output.UnitLibrary/VerboseTransferEventWriter.cs`, `TransferWriteOutVariables.cs` (`FormatHttpVersion`); `-V` per ADR-0021.
- Measure with the OpenSSL-build curl on Linux or macOS and with curl.se's Windows build against an HTTP/2 server (through `Record-CurlExchange.ps1 -NoServer`): `-v`, `-i` and `-w '%{http_version}'`.

## Acceptance criteria

- [ ] Measured first as above; stdout and stderr copied into Notes with varying parts marked.
- [ ] Tests pin each measured output on every platform, with `OSCondition` only where the Windows and OpenSSL texts differ.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for each library changed.

## Notes

## Log

- 2026-09-28: Created.
