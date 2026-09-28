---
id: BL-734
title: Write curl's -v, -i, %{http_version} and -V output for HTTP/3 transfers
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-732]
touches: [Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests, Curl.Output.UnitLibrary, Curl.Output.UnitTests, Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-734 — Write curl's -v, -i, %{http_version} and -V output for HTTP/3 transfers

## Goal

An HTTP/3 transfer writes what curl's official build writes: `-i` status line `HTTP/3 200` with lower-case header names, the `-v` lines for the QUIC connect, ALPN, the stream and the request and response headers, `%{http_version}` `3`, and `-V` listing `HTTP3` among the features (and `ngtcp2`/`nghttp3`-equivalent entries only as ADR-0021 allows), on every platform.

## Context

- Standing rule (root `CLAUDE.md`, "Decisions", 2026-09-28): output text matches the platform's curl where both do the same thing; where the platform's usual build has no HTTP/3 (the Schannel build on Windows), the text of curl.se's official Windows build is the reference. Formatting: `Curl.Output.UnitLibrary/VerboseTransferEventWriter.cs`, `TransferWriteOutVariables.cs` (`FormatHttpVersion`); `-V` per ADR-0021.
- Measure with the official curl build through `Record-CurlExchange.ps1 -NoServer` against an HTTP/3 server (BL-718 records which): `-v`, `-i` and `-w '%{http_version}'`, and `-V`.

## Acceptance criteria

- [ ] Measured first as above; stdout and stderr copied into Notes with varying parts marked.
- [ ] Tests pin each measured output on every platform, normalised as existing `-v` tests are.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for each library changed.

## Notes

## Log

- 2026-09-28: Created.
