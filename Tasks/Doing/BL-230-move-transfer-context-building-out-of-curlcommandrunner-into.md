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
completed:
---
# BL-230 — Move transfer-context building out of CurlCommandRunner into its own class

## Goal

Building a `TransferContext` from `CommandLineOptions` lives in its own class in `Curl.Console`, and `CurlCommandRunner` calls it, with no behaviour change.

## Context

- Filed from the Phase 1 HTTP plan (protocol-architect, 2026-09-26), item W1. Upstream references: https://curl.se/docs/manpage.html and https://curl.se/libcurl/c/libcurl-errors.html; behaviour measured on curl 8.21.0.
- `Curl.Console/CurlCommandRunner.cs` is 656 lines and every Console item touches it; splitting first keeps BL-231 to BL-245 small.
- BL-137 reduces `TransferWithHeaderOutputAsync`'s complexity in the same file; both touch `Curl.Console`, so they run one after the other.

## Acceptance criteria

- [ ] A new class (for example `TransferContextFactory`) builds the context; `CurlCommandRunner.CreateContext` no longer exists or delegates to it.
- [ ] Every existing `Curl.Console.UnitTests` test passes unchanged; the new class has its own tests.
- [ ] `dotnet build Curl.Console -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes and no new test needs `TestCategory=Integration`; `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Console`.

## Notes

- Plan item: W1 in the Phase 1 HTTP plan (2026-09-26); plan keys in this file were replaced by their task IDs.

## Log

- 2026-09-26: Created.
- 2026-09-26: Backlog -> Doing.
