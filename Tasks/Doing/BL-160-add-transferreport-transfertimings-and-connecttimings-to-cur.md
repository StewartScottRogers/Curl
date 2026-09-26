---
id: BL-160
title: Add TransferReport, TransferTimings and ConnectTimings to Curl.Protocol.Abstractions
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-158]
touches: [Curl.Protocol.Abstractions.UnitLibrary, Curl.Protocol.Abstractions.UnitTests]
requirement: none
created: 2026-09-26
completed:
---
# BL-160 — Add TransferReport, TransferTimings and ConnectTimings to Curl.Protocol.Abstractions

## Goal

`TransferResult.Report`, `TransferReport`, `TransferTimings` and `ConnectTimings` exist as the BL-158 ADR states, and existing handlers compile unchanged.

## Context

- Filed from the Phase 1 HTTP plan (protocol-architect, 2026-09-26), item X4. Upstream references: https://curl.se/docs/manpage.html and https://curl.se/libcurl/c/libcurl-errors.html; behaviour measured on curl 8.21.0.
- Implements the BL-158 ADR (read it first). `TransferResult.cs` and `ConnectResult.cs` are the files to extend.

## Acceptance criteria

- [ ] `TransferResult.Report` defaults to null; every existing handler project builds without edits.
- [ ] `ConnectResult.Connected(IConnection)` still compiles and an overload or optional parameter accepts `ConnectTimings`; a test shows both.
- [ ] `dotnet build Curl.Protocol.Abstractions.UnitLibrary -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes and no new test needs `TestCategory=Integration`; `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Protocol.Abstractions`.

## Notes

- Plan item: X4 in the Phase 1 HTTP plan (2026-09-26); plan keys in this file were replaced by their task IDs.

## Log

- 2026-09-26: Created.
- 2026-09-26: Backlog -> Doing.
