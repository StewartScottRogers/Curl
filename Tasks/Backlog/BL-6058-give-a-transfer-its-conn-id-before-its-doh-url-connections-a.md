---
id: BL-6058
title: Give a transfer its conn_id before its --doh-url connections, as curl numbers them
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Console/CurlCommandRunner.cs, Curl.Console/ConnectionIdRecordingTransferEvents.cs, Curl.Console/MultiStateTraceEvents.cs, Curl.Console.UnitTests/DohConnectionIdTests.cs]
requirement: none
created: 2026-11-26
completed:
---
# BL-6058 — Give a transfer its conn_id before its --doh-url connections, as curl numbers them

## Goal

Under `--doh-url`, `%{conn_id}`, `-v`'s `closing connection #N` and the DoH connection numbers match curl's: the first transfer is 0, its DoH connection 1.

## Context

Measured 2026-11-26 with curl 8.18.0 (Windows nghttp2 build and Ubuntu) over three URLs against an HTTP/1.1 DoH server: the transfers are `#0`, `#2`, `#4` and the DoH connections `#1` and `#3`. Curl prints `conn_id` 1, 3, 4 and `closing connection #0`, `#1`, `#2`, with DoH connections `#1`, `#2`: the console takes a transfer's `conn_id` after its DoH look-up. `DohDnsResolver` already uses the number `TakeConnectionId` returns when it is past the next DoH number (ADR-1441).

## Acceptance criteria

- [ ] A unit test runs two `--doh-url` transfers over fakes and shows curl's `conn_id`s and connection numbers.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-26: Created.
