---
id: BL-5983
title: Measure and match curl's connection end when -f fails an HTTP/3 transfer
priority: Low
assignee: Claude
pipeline: direct
depends-on: [BL-5981]
touches: [Curl.Original.Protocol.Http.UnitLibrary/HttpProtocolHandler.ConnectionExchange.cs, Curl.Original.Protocol.Http.UnitLibrary/HttpProtocolHandler.AttemptOutcome.cs, Curl.Original.Protocol.Http.UnitLibrary/HttpProtocolHandler.ResponseExchange.cs, Curl.Original.Protocol.Http.UnitTests/Http3StreamAbortTests.cs, Record-CurlExchange.ps1]
requirement: none
created: 2026-11-25
completed:
---
# BL-5983 — Measure and match curl's connection end when -f fails an HTTP/3 transfer

## Goal

When `-f` fails an HTTP/3 transfer, Curl writes the connection line, ends the stream and treats the next URL as curl does.

## Context

BL-5978 (ADR-1410) matched `-m` over HTTP/3 and left `-f` as it was: Curl writes `closing connection`, sends nothing for the stream and dials again. BL-5977 (ADR-1408) matched `-f` over HTTP/2. The recorder's HTTP/3 origin (BL-5981) answers 200 only; extend it to answer another status.

## Acceptance criteria

- [ ] The Notes record what real curl writes and sends when `-f` fails an HTTP/3 transfer with a second URL to the same origin.
- [ ] Curl's connection line, stream ending and connection reuse match that measurement, pinned by unit tests.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-25: Created.
- 2026-11-25: Backlog -> Doing. Criteria at claim: fb48a309 9289376a 3d836d5e.
