---
id: BL-5392
title: Leave HTTP/2 trailer fields out of %header{} and %{header_json}
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Original.Protocol.Http.UnitLibrary/Http2StreamConnection.cs, Curl.Engine.UnitLibrary/TransferHeaderStore.cs, Curl.Original.Protocol.Http.UnitTests, Curl.Engine.UnitTests]
requirement: none
created: 2026-11-17
completed:
---
# BL-5392 — Leave HTTP/2 trailer fields out of %header{} and %{header_json}

## Goal

Over HTTP/2, `%header{name}` and `%{header_json}` leave trailer fields out, as curl does.

## Context

From the outside fitness review of 2026-11-17 (commit 9561bbe, `preview` build), finding STD-099 (Low, Confirmed, 2 runs). Area: HTTP/2 write-out, Linux build (the Windows REF has no HTTP/2).

**Repro (WSL).** An HTTP/2 prior-knowledge server on 127.0.0.1:44101; for `GET /trail` it sends HEADERS `:status 200`, DATA `hello`, then trailing HEADERS `x-t: trailerval` with END_STREAM.
- `curl -sS -m5 --http2-prior-knowledge -o /dev/null -w "[%header{x-t}] %{header_json}\n" http://127.0.0.1:44101/trail`

**Expected (REF2).** `[] {` and `}` (no trailer). Over HTTP/1.1 chunked trailers, REF and PORT both leave the trailer out.
**Actual (PORT).** `[trailerval] {"x-t":["trailerval"]` and `}`, exit 0.
**Where to start.** `Curl.Original.Protocol.Http.UnitLibrary/Http2StreamConnection.cs` ~lines 513-533 feeds trailer lines from `Http2ResponseHead.FormatTrailers` (`trailers.Write(...)`, ~line 533) with no end-of-head marker, so `Curl.Engine.UnitLibrary/TransferHeaderStore.cs` `EndHead()` (~line 245) never switches `lineOrigin` to `CurlHeaderOrigin.Trailer`. Mark the end of the final head before HTTP/2 trailers.

## Acceptance criteria

- [ ] New tests in `Curl.Original.Protocol.Http.UnitTests` (a new file) prove HTTP/2 trailer lines reach the header store after an end-of-head marker.
- [ ] New tests in `Curl.Engine.UnitTests` (a new file) prove `%header{x-t}` is empty and `%{header_json}` has no `x-t` when the only source is an HTTP/2 trailer.
- [ ] A test proves `-D -` output (trailers included, as REF2 prints them) is unchanged.
- [ ] A test proves HTTP/1.1 chunked trailers behave as before.
- [ ] Tests are platform-neutral (or OS-conditioned where curl differs by platform).
- [ ] Hand check recorded in the Log: the command prints `[] {` and `}`; `-D -` output still matches REF2. Measure real curl with `Record-CurlExchange.ps1` where possible.
- [ ] `dotnet build` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Mock servers must be PowerShell, a C# file-based app or `Record-CurlExchange.ps1`, never Python (CLAUDE.md). The review's server was a Python `h2` one; replace it with hand-written frames.
- Review acceptance check: the command prints `[] {` and `}`; `-D -` output still matches REF2.

## Log

- 2026-11-17: Created.
