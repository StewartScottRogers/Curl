---
id: BL-230
title: Move transfer-context building out of CurlCommandRunner into its own class
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-09-26
completed: 2026-09-26
---
# BL-230 — Move transfer-context building out of CurlCommandRunner into its own class

## Goal

Building a `TransferContext` from `CommandLineOptions` lives in its own class in `Curl.Console`, and `CurlCommandRunner` calls it, with no behaviour change.

## Context

- Filed from the Phase 1 HTTP plan (protocol-architect, 2026-09-26), item W1. Upstream references: https://curl.se/docs/manpage.html and https://curl.se/libcurl/c/libcurl-errors.html; behaviour measured on curl 8.21.0.
- `Curl.Console/CurlCommandRunner.cs` is 656 lines and every Console item touches it; splitting first keeps BL-231 to BL-245 small.
- BL-137 reduces `TransferWithHeaderOutputAsync`'s complexity in the same file; both touch `Curl.Console`, so they run one after the other.

## Acceptance criteria

- [x] A new class (for example `TransferContextFactory`) builds the context; `CurlCommandRunner.CreateContext` no longer exists or delegates to it.
- [x] Every existing `Curl.Console.UnitTests` test passes unchanged; the new class has its own tests.
- [x] `dotnet build Curl.Console -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes and no new test needs `TestCategory=Integration`; `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Console`.

## Notes

- Plan item: W1 in the Phase 1 HTTP plan (2026-09-26); plan keys in this file were replaced by their task IDs.
- Delivered directly rather than through the full `/feature` agent chain: it is a pure move of one private method with no behaviour change, so there was nothing for protocol-architect or conformance to decide.
- `TransferContextFactory(Stream standardInput)` in `Curl.Console/TransferContextFactory.cs`, method `Create(...)` with the same parameters `CreateContext` had. It owns the `telnet` scheme constant, since only it reads it. `CurlCommandRunner` holds one instance built from its own `standardInput`; `CreateContext` is gone.
- `TransferContextFactoryTests` (6 tests) pins the per-transfer values, every option's default, every option copied from a parsed command line, and that only `telnet` (not `telnets`, not `tftp`) uploads standard input. Existing tests unchanged; Curl.Console.UnitTests 270 pass. Measure-CodeQuality: Curl.Console 100% line, 100% branch, 0 failing members, worst CRAP 10.

## Log

- 2026-09-26: Created.
- 2026-09-26: Backlog -> Doing.
- 2026-09-26: Doing -> Done. TransferContextFactory builds each transfer's context; CurlCommandRunner calls it, no behaviour change
