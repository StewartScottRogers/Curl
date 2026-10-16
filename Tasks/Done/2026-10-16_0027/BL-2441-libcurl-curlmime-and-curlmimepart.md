---
id: BL-2441
title: Libcurl: CurlMime and CurlMimePart
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2456, BL-2406]
touches: [Libcurl/Mime/Mime, Libcurl.UnitTests/Mime/Mime, Libcurl/Options/EasyOptionSetters.cs, Libcurl.UnitTests/Options/Http/EasyHttpOptionSettersTests.cs, Libcurl/CLAUDE.md]
requirement: none
created: 2026-10-15
completed: 2026-10-15
---
# BL-2441 — Libcurl: CurlMime and CurlMimePart

## Goal

Libcurl has `CurlMime` and `CurlMimePart` (`curl_mime_init/_free/_addpart/_name/_filename/_type/_encoder/_data/_filedata/_data_cb/_subparts/_headers`) and `CURLOPT_MIMEPOST`.

## Context

ADR-0506; phase 3 of `Documentation/Planning/Curl-Api-And-libcurl.md`. Read the seam as it is in `Curl.Api.UnitLibrary` (EngineContracts, Options, Callbacks, Info, Codes) and the managed engine as it is in `Curl.Engine.UnitLibrary` now, not the plan's guesses, and correct the plan where they differ. Libcurl code and tests live in the area sub-folders named in `touches` (`Libcurl/<Area>` with its tests in `Libcurl.UnitTests/<Area>`) so areas run in parallel lanes; Libcurl tests use fakes of the seam interfaces, never the network. `--ai-help` is untouched (no option changes).

## Acceptance criteria

- [x] Fake-engine tests pin every call and the option wiring.
- [x] `dotnet build` is clean and the fast tests pass with no test deleted, skipped or weakened.
- [x] `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` is unchanged or grown, and every case it lists still passes.
- [x] `Measure-CodeQuality.ps1 -Library Libcurl` reports 0 failing members (100% line and branch coverage, complexity at most 10, CRAP at most 30).
- [x] Every new public member has an XML doc comment, and `Libcurl/CLAUDE.md` or `Curl.Engine.UnitLibrary`'s CLAUDE.md is kept true.

## Notes

- Plan: thin wrappers over the seam's `IMimeBuilder`/`IMimePartBuilder` (the plan's "built on
  `Curl.Core`'s `Multipart`" is the engine's job, BL-2439). `CurlLibrary.MimeInit`, `CurlMime`
  (`AddPart`, `Dispose` = `curl_mime_free`), `CurlMimePart` (`SetName` ... `SetHeaders`, plus a
  `SetData(string)` UTF-8 overload), and `CURLOPT_MIMEPOST` storing the body when set. ADR-0583.
- Touches widened (no task in Doing on `origin/work/dark-factory` names them):
  `Libcurl/Options/EasyOptionSetters.cs` for the new `AddMimePostSetters` partial hook (and
  `AddFormPostSetters` declared for BL-2442 so it need not edit the shared file);
  `Libcurl.UnitTests/Options/Http/EasyHttpOptionSettersTests.cs`, whose test pinned `MIMEPOST` as
  `NotBuiltIn` until this task (now pins only `HTTPPOST`); `Libcurl/CLAUDE.md` for the area paragraph.
- Choice: `CURLOPT_MIMEPOST` snapshots the body (EasyOptions holds a record, not a handle);
  upstream keeps the pointer. Recorded in ADR-0583.
- Measured: `Measure-CodeQuality.ps1 -Library Libcurl` reports 0 failing members; Libcurl.UnitTests 621 passed.

## Log

- 2026-10-15: Created.
- 2026-10-15: Backlog -> Doing.
- 2026-10-15: Doing -> Done. Libcurl has CurlMime and CurlMimePart (curl_mime_init/_free/_addpart and every part setter) over the engine's IMimeBuilder, and CURLOPT_MIMEPOST
