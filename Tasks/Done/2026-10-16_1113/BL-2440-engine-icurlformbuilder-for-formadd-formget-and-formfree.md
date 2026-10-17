---
id: BL-2440
title: Engine: ICurlFormBuilder for formadd, formget and formfree
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2439]
touches: [Curl.Engine.UnitLibrary, Curl.Engine.UnitTests]
requirement: none
created: 2026-10-15
completed: 2026-10-16
---
# BL-2440 — Engine: ICurlFormBuilder for formadd, formget and formfree

## Goal

The engine's `ICurlFormBuilder` implements the deprecated `curl_formadd`, `curl_formget` and `curl_formfree` (`CURLFORM_*`) on top of the MIME builder.

## Context

ADR-0506; phase 3 of `Documentation/Planning/Curl-Api-And-libcurl.md`. Read the seam as it is in `Curl.Api.UnitLibrary` (EngineContracts, Options, Callbacks, Info, Codes) and the managed engine as it is in `Curl.Engine.UnitLibrary` now, not the plan's guesses, and correct the plan where they differ. Libcurl code and tests live in the area sub-folders named in `touches` (`Libcurl/<Area>` with its tests in `Libcurl.UnitTests/<Area>`) so areas run in parallel lanes; Libcurl tests use fakes of the seam interfaces, never the network. `--ai-help` is untouched (no option changes).

## Acceptance criteria

- [x] Engine tests show each `CURLFORM_*` option and the `FormGet` callback bytes.
- [x] `dotnet build` is clean and the fast tests pass with no test deleted, skipped or weakened.
- [x] `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` is unchanged or grown, and every case it lists still passes.
- [x] `Measure-CodeQuality.ps1 -Library Curl.Engine.UnitLibrary` reports 0 failing members (100% line and branch coverage, complexity at most 10, CRAP at most 30).
- [x] Every new public member has an XML doc comment, and `Libcurl/CLAUDE.md` or `Curl.Engine.UnitLibrary`'s CLAUDE.md is kept true.

## Notes

- 2026-10-16: The earlier run was cut off before any edit; this run did the whole task.
- Decision (ADR-0618): `ManagedFormBuilder` reads each call as curl 8.22.0's `FormAdd` (`FormAddCall`, `FormFieldInfo` nodes, `FormPost.Complete`) and builds `Curl_getformdata`'s parts; `Serialize` (`curl_formget`) builds the body with `Curl.Core`'s `MultipartFormBodyBuilder` and hands over the `Content-Type` line, an empty line and the body in 8192-byte pieces, blocking on the async build as `CurlEasy.Perform` does, since the seam's `Serialize` is synchronous.
- Default taken: a value of the wrong .NET type is `CURL_FORMADD_NULL` (a C caller can only pass a null pointer there), and `CURLFORM_FILENAME` with no value answers `NULL` where upstream would crash.
- The bytes were not measured against real curl: upstream test650 is a `lib650` case the harness does not run, and `curl_formget` has no command-line form. They follow `lib/mime.c`'s top-part layout and `MultipartFormBodyBuilder`'s measured `-F` layout.
- Nothing in the engine sends `EasyHttpOptions.HttpPost` yet, and a `CURLFORM_STREAM` part has no data (the read callback is the easy handle's): filed as BL-2482, after BL-2470.
- `Measure-CodeQuality.ps1 -Library Curl.Engine.UnitLibrary`: 100% line, 100% branch, 0 failing members (after splitting `Complete`, `DataOf` and `FileNameOf`, which first measured complexity 12).

## Log

- 2026-10-15: Created.
- 2026-10-16: Backlog -> Doing.
- 2026-10-16: Doing -> Done. ManagedFormBuilder implements curl_formadd, curl_formget and curl_formfree over the MIME builder; build, fast tests and quality gate green
