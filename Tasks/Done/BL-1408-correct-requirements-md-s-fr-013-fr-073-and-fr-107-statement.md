---
id: BL-1408
title: Correct Requirements.md's FR-013, FR-073 and FR-107 statements that call finished work unbuilt
priority: Normal
assignee: Claude
pipeline: docs
depends-on: []
touches: [Documentation/Product/Requirements.md]
requirement: FR-013
created: 2026-10-03
completed: 2026-10-03
---
# BL-1408 — Correct Requirements.md's FR-013, FR-073 and FR-107 statements that call finished work unbuilt

## Goal

`Documentation/Product/Requirements.md` says of FR-013, FR-073 and FR-107 only what is true of the code today: each is built, with the tests that show it, instead of "Not yet implemented" or "Not yet built" naming tasks that are already in `Tasks/Done`.

## Context

- FR-013 (`--crlf` on a `file://` upload) still says "Not yet implemented end to end: `TransferContextFactory` does not copy the parsed flag onto the transfer context, so `curl --crlf -T` does not convert (task BL-633)". BL-633 is in `Tasks/Done/2026-09-29_1312/`, and `Curl.Console/TransferContextFactory.cs` line 179 sets `ConvertLineEndings = options.ConvertLineEndings`.
- FR-073 (`--compressed`) still says "Not yet implemented: `zstd` - Curl sends `Accept-Encoding: deflate, gzip, br` ... and does not decode a `zstd` body (task BL-861)". BL-861 is in `Tasks/Done/2026-09-30_1736/`; `Curl.Protocol.Http.UnitLibrary/HttpRequestHeadFormatter.cs` line 49 is `AcceptEncoding = "deflate, gzip, br, zstd"`, and `Curl.Zstandard.UnitLibrary` decodes the body (ADR-0287).
- FR-107 (`--log-level`, `--log-file`) and the paragraph above its table (line 345) say "Not yet built: BL-938 and BL-917 to BL-929 build it" and "open tasks BL-938, BL-917 to BL-929"; BL-938, BL-917 and BL-929 are in `Tasks/Done/2026-09-29_1112/` and `Tasks/Done/2026-09-29_1500/`.
- The root `CLAUDE.md` rule "say what it does, do what it says": a document that calls built work unbuilt misleads every agent that reads it.

## Acceptance criteria

- [x] FR-013's row says the conversion is built end to end and names at least one existing test in `Curl.Console.UnitTests` or `Curl.Protocol.File.UnitTests` that shows `--crlf -T` converting (found with `git grep`), with no "Not yet implemented" sentence.
- [x] FR-073's row says `zstd` is advertised and decoded, names the test that pins `Accept-Encoding: deflate, gzip, br, zstd` and one that decodes a `zstd` body, and cites ADR-0287.
- [x] The paragraph before FR-107's table and FR-107's row no longer call BL-938 and BL-917 to BL-929 open; they say the log is built and name a test that shows `--log-level info` writing a line.
- [x] Every test name added exists (`git grep` finds it), and `git grep -n "Not yet" Documentation/Product/Requirements.md` shows no line for these three requirements.

## Notes

- Documentation only; no code changes.
- Delivered directly rather than through align-and-document: four sentence edits in one file. Tests cited: `CurlCommandRunnerTransferOptionTests.RunAsync_UploadToFileUrl_ConvertsLineEndingsOnlyUnderCrlf` (FR-013), `HttpRequestHeadFormatterTests.Format_Compressed_SendsAcceptEncodingAfterAccept` and `HttpContentCodingDecoderTests.Decode_ZstdFrame_WritesItsContent` (FR-073), `CurlCommandRunnerDiagnosticLogTests.RunAsync_LogLevelInfoUnderSilent_WritesTheLinesToStandardError` (FR-107); each found with git grep. No .cs or project file changed, so no build was needed.

## Log

- 2026-10-03: Created.
- 2026-10-03: Backlog -> Doing.
- 2026-10-03: Doing -> Done. Requirements.md FR-013, FR-073 and FR-107 now describe the built work and cite the tests that show it
