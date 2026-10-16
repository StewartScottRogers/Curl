---
id: BL-2415
title: Libcurl: write, read and header callbacks wired through the engine
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2408, BL-2406]
touches: [Libcurl/Callbacks/ReadWrite, Libcurl.UnitTests/Callbacks/ReadWrite, Libcurl/Options/Callbacks, Libcurl/CLAUDE.md, Curl.Engine.UnitLibrary, Curl.Engine.UnitTests]
requirement: none
created: 2026-10-15
completed: 2026-10-15
---
# BL-2415 — Libcurl: write, read and header callbacks wired through the engine

## Goal

CURLOPT_WRITEFUNCTION, READFUNCTION and HEADERFUNCTION (and their pointer-less delegate forms) set through `SetOption`, and the managed engine calls them for body, upload and header bytes with upstream's return values (`CURL_WRITEFUNC_PAUSE`, `CURL_READFUNC_ABORT`, a short write failing with `WriteError`).

## Context

ADR-0506; phase 3 of `Documentation/Planning/Curl-Api-And-libcurl.md`. Read the seam as it is in `Curl.Api.UnitLibrary` (EngineContracts, Options, Callbacks, Info, Codes) and the managed engine as it is in `Curl.Engine.UnitLibrary` now, not the plan's guesses, and correct the plan where they differ. Libcurl code and tests live in the area sub-folders named in `touches` (`Libcurl/<Area>` with its tests in `Libcurl.UnitTests/<Area>`) so areas run in parallel lanes; Libcurl tests use fakes of the seam interfaces, never the network. `--ai-help` is untouched (no option changes).

## Acceptance criteria

- [x] Engine tests transfer through a loopback-free fake connection and show the callbacks receive the exact bytes; Libcurl tests pin the option wiring.
- [x] `dotnet build` is clean and the fast tests pass with no test deleted, skipped or weakened.
- [x] `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` is unchanged or grown, and every case it lists still passes.
- [x] `Measure-CodeQuality.ps1 -Library Libcurl,Curl.Engine.UnitLibrary` reports 0 failing members (100% line and branch coverage, complexity at most 10, CRAP at most 30).
- [x] Every new public member has an XML doc comment, and `Libcurl/CLAUDE.md` or `Curl.Engine.UnitLibrary`'s CLAUDE.md is kept true.

## Notes

- The engine already handed body, header and upload bytes to the three callbacks (BL-2316, BL-2359); what
  was missing was the magic returns and the `Libcurl` rows. Decisions in ADR-0569.
- Engine: `EasyPauseGate` on each `ManagedEasyTransfer`. A `CURL_WRITEFUNC_PAUSE` from the write or header
  callback, or a `CURL_READFUNC_PAUSE` from the read callback, waits until `Pause` clears the direction, then
  hands the same data over again. A `file` URL or the synchronous path answers libcurl's pause-not-supported
  error. `CURL_READFUNC_ABORT` is `AbortedByCallback` with `operation aborted by callback`.
  `CURL_WRITEFUNC_ERROR` was already a short write (`WriteError`).
- Touches: the task named `Libcurl/Callbacks/Data`, but the session's `data/` path rule refuses any folder
  of that name (case-insensitive on Windows), so the area is `Libcurl/Callbacks/ReadWrite` (tests in
  `Libcurl.UnitTests/Callbacks/ReadWrite`). Added `Libcurl/Options/Callbacks` for the one
  `AddCallbackSetters` hub, which calls one partial method per callback area so BL-2416 to BL-2422 each add
  their own part. Also added `Libcurl/CLAUDE.md`, which the acceptance criteria ask to keep true. No task
  in Doing on `origin/work/dark-factory` (BL-2381, BL-2402, BL-2407, BL-2429) names any of these.
- Default taken: a `*DATA` `Stream` installs libcurl's default fwrite/fread over it unless the caller's own
  function is set; any other value is accepted and changes nothing (the closure carries the user data).
- The file-URL pause test checks the code (`WriteError`) only: the file handler words the failure itself.
- Measured: `Measure-CodeQuality.ps1 -Library Libcurl,Curl.Engine.UnitLibrary` gave 100/100 line and branch
  coverage and 0 failing members for both. The fast tests are green. The conformance ratchet was not touched.

## Log

- 2026-10-15: Created.
- 2026-10-15: Backlog -> Doing.
- 2026-10-15: Doing -> Done. Write, read and header callbacks are set through Libcurl SetOption, and the engine honours CURL_WRITEFUNC_PAUSE, CURL_READFUNC_PAUSE and CURL_READFUNC_ABORT
