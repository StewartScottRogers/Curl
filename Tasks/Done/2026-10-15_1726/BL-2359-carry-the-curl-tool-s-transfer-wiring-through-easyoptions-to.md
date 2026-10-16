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
completed: 2026-10-15
---
# BL-2359 — Carry the curl tool's transfer wiring through EasyOptions to ManagedEasyTransfer

## Goal

`ManagedEasyTransfer.PerformAsync` can run a transfer exactly as `CurlCommandRunner`'s serial attempt does today, from `EasyOptions` alone: the tool's own settings `TransferSettingsOf` now hard-codes (`-s`, `-S`, `--fail`/`--fail-with-body`, `--proto`, `--proto-redir`, the `--ssl*` level, `-l`, `--upload-flags`, `--proxy-header`, `-U`, `--happy-eyeballs-timeout-ms`, `-G`/`--url-query`, `-d`), the upload body through the read callback (standard input and `-T` files), the `-C` offset, and engine-wide state shared across handles of one engine (cookies, HSTS, alt-svc, the `%{xfer_id}`/`%{conn_id}` numbering, the connection pool across URLs), with curl's easy error text (`CurlEasyErrorText`) supplied to the engine.

## Context

ADR-0506 phase 2; found by BL-2318, which cannot route `Curl.Console`'s serial transfers through `IEasyTransfer.PerformAsync` byte for byte while `ManagedEasyTransfer` builds its context with libcurl defaults (`TransferSettingsOf`), `Stream.Null` standard input, no diagnostic log and a fresh `EngineTransferState` per perform. Compare `ManagedEasyTransfer.CreateContext` with `CurlCommandRunner.FollowRetryingAsync` and the runner's `createAttemptContext`. Where the public contract lacks an option, add it to the easy option groups in `Curl.Api.UnitLibrary` with its `CURLOPT_*` name; where the setting is the tool's alone (`-s`, `--fail-with-body`), decide in an ADR whether it is an engine-internal hook `Curl.Console` reaches (for example an internal `ManagedCurlEngine` constructor taking tool settings) rather than a public option. Trace events and progress are BL-2360.

## Acceptance criteria

- [x] An ADR records how each tool-only setting reaches the engine ("Decided by Claude under Stewart's delegation").
- [x] `ManagedEasyTransferTests` show a transfer under `--fail`, `--proto`, `-G` and an upload through the read callback giving the same request bytes and `CurlCode` as `TransferPerformer` gives for the same settings today.
- [x] Two handles of one `ManagedCurlEngine` share cookies and a pooled connection (a test shows the second reuses the first's connection and sends its cookie).
- [x] `dotnet build` is clean (warnings are errors).
- [x] `dotnet test --filter "TestCategory!=Integration"` passes and no existing test was deleted, skipped or weakened.
- [x] `powershell -NoProfile -File Measure-CodeQuality.ps1` passes (100% line and branch coverage, complexity <= 10, CRAP <= 30).

## Notes

- 2026-10-15 (lane 1): Decisions, recorded in ADR-0542 (Decided by Claude under Stewart's delegation):
  - Settings libcurl has a `CURLOPT_*` for come from the easy options in `TransferSettingsOf`: `CURLOPT_PROTOCOLS_STR` / `CURLOPT_REDIR_PROTOCOLS_STR` (`--proto`, `--proto-redir`), `CURLOPT_DIRLISTONLY` (`-l`) and `CURLOPT_PROXYHEADER`; `--fail` is `CURLOPT_FAILONERROR`, which `HttpRequestOptionsMapping` already reads. `-G` is the tool's: it builds the URL with the query. No option was missing from `Curl.Api.UnitLibrary`, so neither `Curl.Api` project changed.
  - The tool-only settings (`-s`, `-S`, `--fail-with-body`, `-U`, the `--ssl*` level, `--upload-flags`, `--happy-eyeballs-timeout-ms`, the `-G` query and the rest) reach the engine through the internal `ManagedEasyTransfer.ToolSettings` hook (a whole `EngineTransferSettings`), not public options, so the two paths cannot drift.
  - Engine-wide state is one `ManagedEngineSharedState` per engine: the connection, TLS session and DNS caches, a `CookieStore`, an `HstsTransferPolicy` and a `TransferPerformer` (`%{xfer_id}` numbering, and curl's easy error text through the internal constructor).
  - Uploads go through `CURLOPT_READFUNCTION` via `CallbackReadStream`; with no read callback the upload is empty (the engine has no standard input).
- Measured: `Measure-CodeQuality.ps1 -Library Curl.Engine.UnitLibrary` gave 100% line, 100% branch, 529 members, 0 failing, worst CRAP 10. Fast tests: 0 failures.
- Left for BL-2362 (filed): loading the `-b` files, writing the `-c` jar, alt-svc and HSTS files, `%{conn_id}` and the diagnostic log through the easy handle.

## Log

- 2026-10-15: Created.
- 2026-10-15: Backlog -> Doing.
- 2026-10-15: Doing -> Done. ManagedEasyTransfer runs a transfer from EasyOptions with the tool's settings, read-callback uploads and engine-wide cookies, HSTS, pool and xfer_id
