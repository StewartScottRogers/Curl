---
id: BL-3259
title: Read byte content and repeated bytes in differential case files
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.Differential.UnitLibrary/DifferentialCaseFileReader.cs, Curl.Conformance.Differential.UnitLibrary/Curl.Conformance.Differential.UnitLibrary.csproj, Curl.Conformance.Differential.UnitLibrary/Cases/Smoke/SmokeCases.json, Curl.Conformance.Differential.UnitTests/DifferentialCaseFileReaderTests.cs]
requirement: none
created: 2026-10-23
completed: 2026-10-23
---
# BL-3259 — Read byte content and repeated bytes in differential case files

## Goal

A differential case file can give `stdin` and each of `files` as bytes that are not UTF-8 text (base64) and as one byte string repeated N times, and the library embeds every `Cases/**/*.json` case file so a test or the host reads it by name.

## Context

ADR-0782's Consequences: "a case that needs arbitrary bytes needs a new property". The tester's `cases.py` and `cases_more.py` (in `Z:\repos\Curl.difftest\2026-10-22\`, reference only, never run Python) hold 23 cases with `b"..."` content, among them `b"x" * 2_000_000` (`post-big-expect`) and binary stdin. Suggested shape: `"stdinBase64"`, a `files` value that is an object `{ "base64": "..." }` or `{ "repeat": "x", "count": 2000000 }`, keeping a plain string as UTF-8 text. Add `<EmbeddedResource Include="Cases\**\*.json" />` (or equivalent, AOT-safe) and a reader entry point that reads an embedded case file by name. Split out of BL-3233.

## Acceptance criteria

- [x] `DifferentialCaseFileReader` reads base64 and repeated-byte `stdin` and `files` values, and a plain string stays UTF-8 text, each pinned by a unit test.
- [x] Case files under `Cases/` are embedded in `Curl.Conformance.Differential.UnitLibrary` and readable by name, pinned by a unit test.
- [x] Measure-CodeQuality.ps1 reports 0 failing members for Curl.Conformance.Differential.UnitLibrary; build clean and fast tests green.

## Notes

- Filed by BL-3233's lane-3 run on 2026-10-23 when sizing it.
- Added `Curl.Conformance.Differential.UnitLibrary/Cases/Smoke/SmokeCases.json` to touches: the embedding criterion needs one embedded case file to read, and no task in Doing on origin/work/dark-factory names it (BL-3261/BL-3262 add their own files under `Cases/CliHttp`).
- Decided (ADR-0785): one content shape for both `stdin` and `files` (string = UTF-8, `{"base64"}`, `{"repeat","count"}`) rather than a separate `stdinBase64`; embedded files keep the SDK default manifest name and `ReadEmbedded("Dir/File.json")` maps `/` to `.`, which is the same on every platform.
- Measured: Measure-CodeQuality.ps1 -Library Curl.Conformance.Differential.UnitLibrary reports 0 failing members; the library's 219 unit tests pass. Tests use no file system or OS text, so they are platform-neutral.

## Log

- 2026-10-23: Created.
- 2026-10-23: Backlog -> Doing. Criteria at claim: 0097e498 0bd19de4 61bbcf39.
- 2026-10-23: Doing -> Done. Differential case files read base64 and repeated-byte stdin/files content, and embedded Cases/**/*.json files are read by path
