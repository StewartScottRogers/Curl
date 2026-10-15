---
id: BL-2359
title: Carry the curl tool's transfer wiring through EasyOptions to ManagedEasyTransfer
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2316]
touches: [Curl.Api.UnitLibrary, Curl.Api.UnitTests, Curl.Engine.UnitLibrary, Curl.Engine.UnitTests]
requirement: none
created: 2026-10-15
completed:
---
# BL-2359 — Carry the curl tool's transfer wiring through EasyOptions to ManagedEasyTransfer

## Goal

`ManagedEasyTransfer.PerformAsync` can run a transfer exactly as `CurlCommandRunner`'s serial attempt does today, from `EasyOptions` alone: the tool's own settings `TransferSettingsOf` now hard-codes (`-s`, `-S`, `--fail`/`--fail-with-body`, `--proto`, `--proto-redir`, the `--ssl*` level, `-l`, `--upload-flags`, `--proxy-header`, `-U`, `--happy-eyeballs-timeout-ms`, `-G`/`--url-query`, `-d`), the upload body through the read callback (standard input and `-T` files), the `-C` offset, and engine-wide state shared across handles of one engine (cookies, HSTS, alt-svc, the `%{xfer_id}`/`%{conn_id}` numbering, the connection pool across URLs), with curl's easy error text (`CurlEasyErrorText`) supplied to the engine.

## Context

ADR-0506 phase 2; found by BL-2318, which cannot route `Curl.Console`'s serial transfers through `IEasyTransfer.PerformAsync` byte for byte while `ManagedEasyTransfer` builds its context with libcurl defaults (`TransferSettingsOf`), `Stream.Null` standard input, no diagnostic log and a fresh `EngineTransferState` per perform. Compare `ManagedEasyTransfer.CreateContext` with `CurlCommandRunner.FollowRetryingAsync` and the runner's `createAttemptContext`. Where the public contract lacks an option, add it to the easy option groups in `Curl.Api.UnitLibrary` with its `CURLOPT_*` name; where the setting is the tool's alone (`-s`, `--fail-with-body`), decide in an ADR whether it is an engine-internal hook `Curl.Console` reaches (for example an internal `ManagedCurlEngine` constructor taking tool settings) rather than a public option. Trace events and progress are BL-2360.

## Acceptance criteria

- [ ] An ADR records how each tool-only setting reaches the engine ("Decided by Claude under Stewart's delegation").
- [ ] `ManagedEasyTransferTests` show a transfer under `--fail`, `--proto`, `-G` and an upload through the read callback giving the same request bytes and `CurlCode` as `TransferPerformer` gives for the same settings today.
- [ ] Two handles of one `ManagedCurlEngine` share cookies and a pooled connection (a test shows the second reuses the first's connection and sends its cookie).
- [ ] `dotnet build` is clean (warnings are errors).
- [ ] `dotnet test --filter "TestCategory!=Integration"` passes and no existing test was deleted, skipped or weakened.
- [ ] `powershell -NoProfile -File Measure-CodeQuality.ps1` passes (100% line and branch coverage, complexity <= 10, CRAP <= 30).

## Notes

## Log

- 2026-10-15: Created.
