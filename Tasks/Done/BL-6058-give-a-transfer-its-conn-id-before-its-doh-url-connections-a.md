---
id: BL-6058
title: Give a transfer its conn_id before its --doh-url connections, as curl numbers them
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Console/CurlCommandRunner.cs, Curl.Console/CurlComposition.cs, Curl.Console/CLAUDE.md, Curl.Console/ConnectionIdRecordingTransferEvents.cs, Curl.Console/MultiStateTraceEvents.cs, Curl.Console.UnitTests/DohConnectionIdTests.cs, Curl.Engine.UnitLibrary/TransferPerformer.cs, Curl.Engine.UnitTests/TransferPerformerTests.cs, Curl.Networking.UnitLibrary/ConnectionCache.cs, Curl.Networking.UnitLibrary/ConnectionNumberReservation.cs, Curl.Networking.UnitLibrary/PoolingConnector.cs, Curl.Networking.UnitLibrary/CLAUDE.md, Curl.Networking.UnitTests/ConnectionCacheSubTransferNumberingTests.cs, Documentation/Planning/Decisions/ADR-1441-doh-url-keeps-its-connections-for-later-queries.md]
requirement: none
created: 2026-11-26
completed: 2026-11-26
---
# BL-6058 — Give a transfer its conn_id before its --doh-url connections, as curl numbers them

## Goal

Under `--doh-url`, `%{conn_id}`, `-v`'s `closing connection #N` and the DoH connection numbers match curl's: the first transfer is 0, its DoH connection 1.

## Context

Measured 2026-11-26 with curl 8.18.0 (Windows nghttp2 build and Ubuntu) over three URLs against an HTTP/1.1 DoH server: the transfers are `#0`, `#2`, `#4` and the DoH connections `#1` and `#3`. Curl prints `conn_id` 1, 3, 4 and `closing connection #0`, `#1`, `#2`, with DoH connections `#1`, `#2`: the console takes a transfer's `conn_id` after its DoH look-up. `DohDnsResolver` already uses the number `TakeConnectionId` returns when it is past the next DoH number (ADR-1441).

## Acceptance criteria

- [x] A unit test runs two `--doh-url` transfers over fakes and shows curl's `conn_id`s and connection numbers.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- `touches` widened: `-v`'s `#N` is the pool's number (`ConnectionCache`), not `%{conn_id}`, and it was
  taken after the connect, so the fix needs `ConnectionCache`/`PoolingConnector`, `TransferPerformer` and
  `CurlComposition` too. No other task in Doing named any of them (checked on origin/work/dark-factory).
- Each DoH connection now takes a number from both counts, the transfer it resolves for taking its own
  first (ADR-1455). Over HTTP/1.1 TLS this gives transfers `#0`, `#2`, `#4` and DoH `#1`, `#3`, as measured.
- `DohConnectionIdTests` runs three transfers over fakes with a plain-http DoH server (two DoH connections
  for the first name): `[0][3][4]` when they are kept, `[0][3][6]` when each closes. The DoH numbers
  follow from the gaps; the dialing runner writes no DoH `-v` lines.
- `Measure-CodeQuality.ps1` for the three changed libraries: 0 failing members.

## Log

- 2026-11-26: Created.
- 2026-11-26: Backlog -> Doing. Criteria at claim: 6d06fcd3 3d836d5e.
- 2026-11-26: Doing -> Done. A --doh-url transfer now takes its conn_id and -v connection number before its DoH connections (ADR-1455).
