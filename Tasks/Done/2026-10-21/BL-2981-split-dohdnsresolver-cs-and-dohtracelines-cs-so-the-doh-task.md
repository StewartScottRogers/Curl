---
id: BL-2981
title: Split DohDnsResolver.cs and DohTraceLines.cs so the DoH tasks can run in parallel
priority: High
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Networking.UnitLibrary/DohDnsResolver.cs, Curl.Networking.UnitLibrary/DohTraceLines.cs, Curl.Networking.UnitTests/DohDnsResolverTraceTests.cs]
requirement: none
created: 2026-10-21
completed: 2026-10-21
---
# BL-2981 — Split DohDnsResolver.cs and DohTraceLines.cs so the DoH tasks can run in parallel

## Goal

`task-board.ps1 hotspots` no longer lists `DohDnsResolver.cs`, `DohTraceLines.cs` or
`DohDnsResolverTraceTests.cs`, because the waiting DoH tasks each touch a file of their own.

## Context

On 2026-10-21 six of nine lanes sat idle ("every ready task overlaps one in Doing"), and the board's
hotspots were `Curl.Networking.UnitLibrary/DohDnsResolver.cs` (7 tasks: BL-2923, BL-2937, BL-2969,
BL-2970, BL-2972, BL-2973, BL-2979), `DohTraceLines.cs` (6) and
`Curl.Networking.UnitTests/DohDnsResolverTraceTests.cs` (5). CLAUDE.md: a file many tasks touch is
split as its own High task rather than waited on (Stewart, 2026-10-21: "always function in a highly
parallel fashion").

## Acceptance criteria

- [x] `DohDnsResolver.cs` and `DohTraceLines.cs` are split, by partial class or by extracting types,
      along the lines the waiting tasks work on: request encoding, answer decoding (NXDOMAIN, CNAME,
      TTL), HTTP status failures, sub-transfer trace lines, and HTTPS-RR trace gating. Behaviour is
      unchanged.
- [x] `DohDnsResolverTraceTests.cs` is split the same way, so each area's tests are in their own file.
- [x] Each waiting DoH task's `touches` (BL-2923, BL-2937, BL-2969, BL-2970, BL-2972, BL-2973,
      BL-2979) names only the new files its work lands in, edited in Backlog with the task board's
      rules.
- [x] `task-board.ps1 hotspots` lists none of the three files; `dotnet build` is clean, the fast
      tests pass and `Measure-CodeQuality.ps1` passes for `Curl.Networking.UnitLibrary`.

## Notes
- Split by partial class, behaviour unchanged. `DohDnsResolver`: core (constructors, `ResolveAsync`), `.RequestEncoding` (HTTPS query name, POST head), `.AnswerDecoding` (`ResolvedAddresses`, `DecodeAnswer`), `.Exchange` (`QueryAsync`, `ExchangeAsync`), `.SubTransferTrace` (`RunAddressQueriesAsync`, `AddressQueries`), `.HttpsRecord` (HTTPS public methods). `DohTraceLines`: core (`Report`), `.AnswerDecoding`, `.ExchangeFailures` (new `ExchangeFailureLines`), `.HttpsRecord`. `DohDnsResolverTraceTests`: core (constructors, helpers), `.AnswerDecoding`, `.ExchangeFailures`.
- Waiting tasks' `touches` now name the new files: BL-2923 (RequestEncoding, new `DohRequestEncodingTests.cs`), BL-2937 (`TcpConnector.cs`, new `TcpConnectorProxyRedialTests.cs`; it never touched DoH), BL-2969 and BL-2970 (AnswerDecoding), BL-2972 (Exchange, ExchangeFailures), BL-2973 (SubTransferTrace), BL-2979 (HttpsRecord).
- `task-board.ps1 hotspots` lists no DoH file; Curl.Networking.UnitTests pass (3322); Measure-CodeQuality for Curl.Networking.UnitLibrary: 0 failing members.

## Log

- 2026-10-21: Created.
- 2026-10-21: Backlog -> Doing. Criteria at claim: 8482f913 9da47be0 725feaad 2c6c346b.
- 2026-10-21: Doing -> Done. DohDnsResolver, DohTraceLines and DohDnsResolverTraceTests split into partials; waiting DoH tasks retouched
